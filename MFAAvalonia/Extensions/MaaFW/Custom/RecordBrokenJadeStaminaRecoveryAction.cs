using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public sealed class RecordBrokenJadeStaminaRecoveryAction : IMaaCustomAction
{
    private readonly MaaProcessor _owner;

    public string Name { get; set; } = nameof(RecordBrokenJadeStaminaRecoveryAction);

    public RecordBrokenJadeStaminaRecoveryAction(MaaProcessor owner)
    {
        _owner = owner;
    }

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var parameters = ActionParamHelper.Parse(args.ActionParam);
            var maximum = (int?)parameters["maximum"] ?? -1;
            if (maximum < -1)
                throw new ArgumentOutOfRangeException(nameof(maximum), "最大次数必须是 -1 或非负整数。");

            if (!_owner.RecordBrokenJadeStaminaRecoveryForActiveTask(maximum, out var count, out var limitReached))
            {
                LoggerHelper.Warning("[碎玉换体计数] 当前没有可统计的常驻出阵任务。");
                _owner.AddLog("[碎玉换体计数] 当前没有可统计的常驻出阵任务。");
                return false;
            }

            LoggerHelper.Info($"[碎玉换体计数] 当前任务项已成功换体 {count} 次，最大次数 {maximum}。");
            if (limitReached)
                _owner.AddLog($"[碎玉换体计数] 已达到本次任务最大换体次数 {maximum}，结束当前任务项。");

            return true;
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"[碎玉换体计数] 记录失败：{ex.Message}", ex);
            _owner.AddLog($"[碎玉换体计数] 记录失败：{ex.Message}");
            return false;
        }
    }
}
