using CommunityToolkit.Mvvm.ComponentModel;
using MaaFramework.Binding;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.ViewModels.Pages;
using MFAAvalonia.Views.Windows;
using Avalonia.Media;
using MFAAvalonia.Helper;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Helper.ValueType;

public partial class MFATask : ObservableObject
{
    public enum MFATaskType
    {
        MFA,
        MAAFW
    }

    public enum MFATaskStatus
    {
        NOT_STARTED,
        STOPPING,
        STOPPED,
        SUCCEEDED,
        FAILED
    }

    public readonly record struct RunResult(MFATaskStatus Status, bool ContinueQueue = false);

    [ObservableProperty] private string? _name = string.Empty;
    [ObservableProperty] private MFATaskType _type = MFATaskType.MFA;
    [ObservableProperty] private int _count = 1;
    [ObservableProperty] private Func<Task> _action;
    public Func<Task<MaaJobStatus>>? MaaAction { get; set; }
    public Func<Task<MaaJobStatus>>? StopCurrentIterationAsync { get; set; }
    // [ObservableProperty] private Dictionary<string, MaaNode> _tasks = new();
    [ObservableProperty] private bool _isUpdateRelated;

    public TaskQueueViewModel? OwnerViewModel { get; set; }
    public DragItemViewModel? SourceItem { get; set; }
    public long RunId { get; set; }
    public bool ContinueOnError { get; set; }

    private int _battleVictoryCount;
    private int _battleFailureCount;

    public bool IsResidentBattleTask => string.Equals(
        SourceItem?.InterfaceItem?.Entry,
        "ResidentBattle",
        StringComparison.Ordinal);

    public int BattleVictoryCount => Volatile.Read(ref _battleVictoryCount);
    public int BattleFailureCount => Volatile.Read(ref _battleFailureCount);

    public void RecordBattleOutcome(bool succeeded)
    {
        if (!IsResidentBattleTask)
            return;

        if (succeeded)
            Interlocked.Increment(ref _battleVictoryCount);
        else
            Interlocked.Increment(ref _battleFailureCount);
    }

    private int _brokenJadeStaminaRecoveryCount;

    public bool RecordBrokenJadeStaminaRecovery(int maximum, out int count, out bool limitReached)
    {
        count = 0;
        limitReached = false;
        if (!IsResidentBattleTask || maximum < -1)
            return false;

        count = Interlocked.Increment(ref _brokenJadeStaminaRecoveryCount);
        limitReached = maximum >= 0 && count >= maximum;
        if (limitReached)
            RequestEarlyCompletion($"已达到碎玉换体最大次数（{count}/{maximum}）");

        return true;
    }

    private readonly object _earlyCompletionLock = new();
    private string? _earlyCompletionReason;
    private bool _earlyCompletionRequested;
    private TaskCompletionSource _earlyCompletionSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 请求停止当前执行，并取消该队列项的剩余重复次数。
    /// </summary>
    public bool RequestEarlyCompletion(string? reason)
    {
        lock (_earlyCompletionLock)
        {
            if (_earlyCompletionRequested)
                return true;

            _earlyCompletionReason = reason;
            _earlyCompletionRequested = true;
            _earlyCompletionSignal.TrySetResult();
            return true;
        }
    }

    private bool TryConsumeEarlyCompletion(out string? reason)
    {
        lock (_earlyCompletionLock)
        {
            if (!_earlyCompletionRequested)
            {
                reason = null;
                return false;
            }

            reason = _earlyCompletionReason;
            _earlyCompletionReason = null;
            _earlyCompletionRequested = false;
            _earlyCompletionSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }

    public async Task<RunResult> Run(CancellationToken token)
    {
        var instanceId = OwnerViewModel?.Processor.InstanceId;
        if (instanceId != null)
            TelemetryService.StartTask(instanceId, this);

        RunResult Complete(MFATaskStatus status, bool continueQueue = false)
        {
            if (IsResidentBattleTask)
            {
                OwnerViewModel?.Processor.AddLogByKey(
                    LangKeys.ResidentBattleResultSummary,
                    (Avalonia.Media.IBrush?)null,
                    true,
                    true,
                    BattleVictoryCount.ToString(),
                    BattleFailureCount.ToString());
            }

            if (instanceId != null)
                TelemetryService.FinishTask(instanceId, this, status, status == MFATaskStatus.FAILED);
            return new RunResult(status, continueQueue);
        }

        void MarkFailed(string? detail = null)
        {
            var taskName = LanguageHelper.GetLocalizedString(Name);
            OwnerViewModel?.AddLogByKey(
                LangKeys.TaskFailedWithName,
                Brushes.OrangeRed,
                changeColor: false,
                transformKey: true,
                taskName);
            OwnerViewModel?.MarkTaskFailed(SourceItem, RunId, detail);
        }

        var stoppingForEarlyCompletion = false;
        try
        {
            Interlocked.Exchange(ref _battleVictoryCount, 0);
            Interlocked.Exchange(ref _battleFailureCount, 0);
            Interlocked.Exchange(ref _brokenJadeStaminaRecoveryCount, 0);
            var infinite = Count < 0;
            if (Count < 0)
                Count = int.MaxValue;
            OwnerViewModel?.MarkTaskRunning(SourceItem, RunId);
            var hasFailed = false;
            string? failureMessage = null;
            for (int i = 0; i < Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Type == MFATaskType.MAAFW)
                {
                    OwnerViewModel?.AddLogByKey(LangKeys.TaskStart, (Avalonia.Media.IBrush?)null, true, true, LanguageHelper.GetLocalizedString(Name));
                    OwnerViewModel?.SetCurrentTaskName(LanguageHelper.GetLocalizedString(Name));
                }
                MaaJobStatus? jobStatus = null;
                if (MaaAction != null)
                {
                    Task completionRequested;
                    lock (_earlyCompletionLock)
                        completionRequested = _earlyCompletionSignal.Task;

                    var iteration = MaaAction();
                    if (StopCurrentIterationAsync != null
                        && await Task.WhenAny(iteration, completionRequested) == completionRequested
                        && !iteration.IsCompleted)
                    {
                        token.ThrowIfCancellationRequested();
                        stoppingForEarlyCompletion = true;
                        // 在调度层等待停止，避免自定义动作回调等待其自身退出。
                        var stopStatus = await StopCurrentIterationAsync()
                            .WaitAsync(TimeSpan.FromSeconds(30), token);
                        if (stopStatus != MaaJobStatus.Succeeded)
                            throw new InvalidOperationException("结束当前任务时底层执行器未能停止。");
                    }

                    // 底层尚未退出时不能启动后续队列项。
                    jobStatus = await iteration;
                    stoppingForEarlyCompletion = false;
                    token.ThrowIfCancellationRequested();
                }
                else
                {
                    await Action();
                    token.ThrowIfCancellationRequested();
                }

                if (TryConsumeEarlyCompletion(out var earlyCompletionReason))
                {
                    var taskName = string.IsNullOrWhiteSpace(SourceItem?.Name)
                        ? LanguageHelper.GetLocalizedString(Name)
                        : SourceItem.Name;
                    var message = $"[{taskName}] 任务结束 原因：{earlyCompletionReason}";
                    LoggerHelper.Info(message);
                    OwnerViewModel?.Processor.AddLog(message, (IBrush?)null);
                    break;
                }

                if (jobStatus is { } status && status != MaaJobStatus.Succeeded)
                {
                    hasFailed = true;
                    failureMessage = status.ToString();
                    if (!ContinueOnError)
                    {
                        MarkFailed(failureMessage);
                        return Complete(MFATaskStatus.FAILED);
                    }
                }

                OwnerViewModel?.MarkTaskIterationCompleted(SourceItem, RunId);
                // 有限重复任务每轮完成后输出进度；单次任务与无限任务不输出此进度。
                if (!infinite && Count > 1 && Type == MFATaskType.MAAFW)
                {
                    OwnerViewModel?.AddLogByKey(
                        LangKeys.TaskRoundComplete,
                        (Avalonia.Media.IBrush?)null,
                        true,
                        true,
                        LanguageHelper.GetLocalizedString(Name),
                        (i + 1).ToString(),
                        Count.ToString());
                }
            }
            if (hasFailed)
            {
                MarkFailed(failureMessage);
                return Complete(MFATaskStatus.FAILED, ContinueOnError);
            }
            else
                OwnerViewModel?.MarkTaskSucceeded(SourceItem, RunId);
            return Complete(MFATaskStatus.SUCCEEDED);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            OwnerViewModel?.MarkTaskStopped(SourceItem, RunId);
            return Complete(MFATaskStatus.STOPPED);
        }
        catch (MaaJobStatusException ex)
        {
            MarkFailed(ex.Message);
            LoggerHelper.Error($"任务执行失败：{LanguageHelper.GetLocalizedString(Name)}");
            return Complete(MFATaskStatus.FAILED, ContinueOnError && !stoppingForEarlyCompletion);
        }
        catch (OperationCanceledException)
        {
            OwnerViewModel?.MarkTaskStopped(SourceItem, RunId);
            return Complete(MFATaskStatus.STOPPED);
        }
        catch (InvalidOperationException ex) when (
            string.Equals(ex.Message, MaaProcessor.ConnectionFailedAfterAllRetriesMessage, StringComparison.Ordinal))
        {
            MarkFailed(ex.Message);
            LoggerHelper.Warning($"连接任务已在重试耗尽后结束：任务={LanguageHelper.GetLocalizedString(Name)}");
            return Complete(MFATaskStatus.FAILED, ContinueOnError);
        }
        catch (Exception ex)
        {
            MarkFailed(ex.Message);
            LoggerHelper.Error($"任务执行异常：任务={LanguageHelper.GetLocalizedString(Name)}，原因={ex.Message}", ex);
            return Complete(MFATaskStatus.FAILED, ContinueOnError && !stoppingForEarlyCompletion);
        }
    }
}
