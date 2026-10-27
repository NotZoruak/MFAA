using MaaFramework.Binding;
using MFAAvalonia.Helper.ValueType;
using Xunit;

namespace MFAAvalonia.Tests;

public sealed class TaskEarlyCompletionTests
{
    [Theory(DisplayName = "主动结束当前任务时底层中断状态应归为正常完成")]
    [InlineData(MaaJobStatus.Invalid)]
    [InlineData(MaaJobStatus.Failed)]
    public async Task Run_主动结束不会误报失败(MaaJobStatus status)
    {
        var task = new MFATask { Name = "体力不足", Count = 30 };
        var calls = 0;
        task.MaaAction = () =>
        {
            calls++;
            task.RequestEarlyCompletion("无可用体力回复道具");
            return Task.FromResult(status);
        };

        var result = await task.Run(CancellationToken.None);

        Assert.Equal(MFATask.MFATaskStatus.SUCCEEDED, result.Status);
        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "用户取消优先于正常完成请求")]
    public async Task Run_用户取消仍然停止队列()
    {
        using var cancellation = new CancellationTokenSource();
        var task = new MFATask { Name = "体力不足", Count = 30 };
        task.MaaAction = () =>
        {
            task.RequestEarlyCompletion("无可用体力回复道具");
            cancellation.Cancel();
            return Task.FromResult(MaaJobStatus.Succeeded);
        };

        var result = await task.Run(cancellation.Token);

        Assert.Equal(MFATask.MFATaskStatus.STOPPED, result.Status);
        Assert.False(result.ContinueQueue);
    }

    [Fact(DisplayName = "循环中的当前任务收到完成请求后停止且等待底层退出")]
    public async Task Run_停止当前执行且只停止一次()
    {
        var pipeline = new TaskCompletionSource<MaaJobStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var task = new MFATask { Name = "体力不足", Count = 30 };
        var iterations = 0;
        var stops = 0;
        task.MaaAction = () =>
        {
            iterations++;
            return pipeline.Task;
        };
        task.StopCurrentIterationAsync = () =>
        {
            Interlocked.Increment(ref stops);
            stopStarted.TrySetResult();
            return Task.FromResult(MaaJobStatus.Succeeded);
        };
        var running = task.Run(cancellation.Token);

        try
        {
            Assert.True(task.RequestEarlyCompletion("无可用体力回复道具"));
            Assert.True(task.RequestEarlyCompletion("重复请求"));
            await stopStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.False(running.IsCompleted);

            pipeline.SetResult(MaaJobStatus.Failed);
            var result = await running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Equal(MFATask.MFATaskStatus.SUCCEEDED, result.Status);
            Assert.Equal(1, iterations);
            Assert.Equal(1, stops);
            Assert.False(cancellation.IsCancellationRequested);

            var nextIterations = 0;
            var nextTask = new MFATask
            {
                Name = "后续任务",
                MaaAction = () =>
                {
                    nextIterations++;
                    return Task.FromResult(MaaJobStatus.Succeeded);
                }
            };
            Assert.Equal(MFATask.MFATaskStatus.SUCCEEDED, (await nextTask.Run(cancellation.Token)).Status);
            Assert.Equal(1, nextIterations);
        }
        finally
        {
            pipeline.TrySetResult(MaaJobStatus.Succeeded);
            await running;
        }
    }

    [Theory(DisplayName = "无法停止底层执行器时不得误报完成或继续后续队列")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Run_底层停止失败必须停止队列(bool throws)
    {
        var pipeline = new TaskCompletionSource<MaaJobStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new MFATask
        {
            Name = "体力不足",
            ContinueOnError = true,
            MaaAction = () => pipeline.Task,
            StopCurrentIterationAsync = () => throws
                ? Task.FromException<MaaJobStatus>(new InvalidOperationException("停止失败"))
                : Task.FromResult(MaaJobStatus.Failed)
        };
        var running = task.Run(CancellationToken.None);

        try
        {
            task.RequestEarlyCompletion("无可用体力回复道具");
            var result = await running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Equal(MFATask.MFATaskStatus.FAILED, result.Status);
            Assert.False(result.ContinueQueue);
        }
        finally
        {
            pipeline.TrySetResult(MaaJobStatus.Succeeded);
            await running;
        }
    }

    [Fact(DisplayName = "自然完成的任务不调用底层停止且保留正常重复次数")]
    public async Task Run_自然完成不停止执行器()
    {
        var iterations = 0;
        var stops = 0;
        var task = new MFATask
        {
            Name = "正常任务",
            Count = 3,
            MaaAction = () =>
            {
                iterations++;
                return Task.FromResult(MaaJobStatus.Succeeded);
            },
            StopCurrentIterationAsync = () =>
            {
                stops++;
                return Task.FromResult(MaaJobStatus.Succeeded);
            }
        };

        var result = await task.Run(CancellationToken.None);

        Assert.Equal(MFATask.MFATaskStatus.SUCCEEDED, result.Status);
        Assert.Equal(3, iterations);
        Assert.Equal(0, stops);
    }
}
