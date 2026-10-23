using MFAAvalonia.Helper;
using Xunit;

namespace MFAAvalonia.Tests;

public sealed class TaskStopLifecycleTests
{
    [Fact(DisplayName = "已取消的重复停止提前返回后仍恢复按钮")]
    public async Task RunAsync_提前返回仍收尾()
    {
        var lifecycle = new TaskStopLifecycle();
        var enabled = true;
        var transitions = new List<bool>();

        await lifecycle.RunAsync(() => Task.CompletedTask, stopping =>
        {
            enabled = !stopping;
            transitions.Add(enabled);
        });

        Assert.True(enabled);
        Assert.Equal([false, true], transitions);
        Assert.False(lifecycle.IsStopping);
    }

    [Fact(DisplayName = "异步停止尚未结束时重复请求不能提前解锁按钮")]
    public async Task RunAsync_等待真正完成且合并重复停止()
    {
        var lifecycle = new TaskStopLifecycle();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enabled = true;
        var calls = 0;
        var stopping = lifecycle.RunAsync(async () =>
        {
            calls++;
            await completion.Task;
        }, active => enabled = !active);

        Assert.False(enabled);
        Assert.False(stopping.IsCompleted);
        await lifecycle.RunAsync(() =>
        {
            calls++;
            return Task.CompletedTask;
        }, active => enabled = !active);
        Assert.False(enabled);
        Assert.Equal(1, calls);

        completion.SetResult();
        await stopping;
        Assert.True(enabled);
        Assert.False(lifecycle.IsStopping);
    }

    [Theory(DisplayName = "异步停止失败或取消后恢复按钮并允许再次停止")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_异常和取消都收尾(bool cancelled)
    {
        var lifecycle = new TaskStopLifecycle();
        var enabled = true;
        var failure = cancelled ? (Exception)new OperationCanceledException() : new InvalidOperationException("停止失败");

        Assert.Same(failure, await Record.ExceptionAsync(() => lifecycle.RunAsync(async () =>
        {
            await Task.Yield();
            throw failure;
        }, active => enabled = !active)));

        Assert.True(enabled);
        Assert.False(lifecycle.IsStopping);
        var called = false;
        await lifecycle.RunAsync(() =>
        {
            called = true;
            return Task.CompletedTask;
        }, active => enabled = !active);
        Assert.True(called);
        Assert.True(enabled);
    }
}
