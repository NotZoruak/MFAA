using MFAAvalonia.Helper;
using Xunit;

namespace MFAAvalonia.Tests;

public sealed class TaskRecoveryMonitorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    [Theory(DisplayName = "控制器成功输入使持续超过两分钟的 Agent 战斗保持活跃")]
    [InlineData("click")]
    [InlineData("swipe")]
    [InlineData("multi_swipe")]
    [InlineData("touch_down")]
    [InlineData("touch_move")]
    [InlineData("touch_up")]
    [InlineData("click_key")]
    [InlineData("key_down")]
    [InlineData("key_up")]
    public void RecordControllerAction_成功输入同时刷新活动和回调(string action)
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);

        for (var seconds = 60; seconds <= 300; seconds += 60)
        {
            monitor.RecordControllerAction(action, TimeSpan.FromSeconds(seconds));
            Assert.Null(monitor.GetReason(TimeSpan.FromSeconds(seconds + 5), Timeout, enabled: true, waiting: false));
        }
    }

    [Theory(DisplayName = "截图和连接成功不算输入活动")]
    [InlineData("screencap")]
    [InlineData("connect")]
    [InlineData("unknown")]
    public void RecordControllerAction_非输入事件不能掩盖超时(string action)
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);
        monitor.RecordCallback(TimeSpan.FromSeconds(119));
        monitor.RecordControllerAction(action, TimeSpan.FromSeconds(119));

        Assert.StartsWith("任务无点击", monitor.GetReason(Timeout, Timeout, enabled: true, waiting: false));
    }

    [Fact(DisplayName = "控制器活动不影响节点重复点击检测")]
    public void RecordControllerAction_保留重复点击检测()
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);
        for (var i = 1; i <= 99; i++)
        {
            var now = TimeSpan.FromSeconds(i);
            monitor.RecordControllerAction("touch_down", now);
            monitor.RecordControllerAction("touch_up", now);
            monitor.FeedAction("固定节点", "Click", i, 0, now);
        }

        Assert.StartsWith("画面冻结", monitor.GetReason(TimeSpan.FromSeconds(99), Timeout, enabled: true, waiting: false));
    }

    [Fact(DisplayName = "120 秒无任务回调时判定模拟器无响应")]
    public void GetReason_无任务回调超过阈值时判定模拟器无响应()
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);

        var reason = monitor.GetReason(Timeout, Timeout, enabled: true, waiting: false);

        Assert.True(TaskRecoveryMonitor.IsEmulatorUnresponsiveReason(reason));
    }

    [Fact(DisplayName = "有回调但 120 秒无成功点击时触发游戏恢复")]
    public void GetReason_回调持续但没有成功点击时触发恢复()
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);
        monitor.RecordCallback(TimeSpan.FromSeconds(60));
        monitor.RecordCallback(TimeSpan.FromSeconds(119));

        var reason = monitor.GetReason(Timeout, Timeout, enabled: true, waiting: false);

        Assert.StartsWith("任务无点击", reason);
        Assert.False(TaskRecoveryMonitor.IsEmulatorUnresponsiveReason(reason));
    }

    [Fact(DisplayName = "等待期间暂停并重置超时基线")]
    public void GetReason_等待窗口重置超时基线()
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);

        Assert.Null(monitor.GetReason(Timeout, Timeout, enabled: true, waiting: true));
        Assert.Null(monitor.GetReason(TimeSpan.FromSeconds(239), Timeout, enabled: true, waiting: false));
        Assert.True(TaskRecoveryMonitor.IsEmulatorUnresponsiveReason(
            monitor.GetReason(TimeSpan.FromSeconds(240), Timeout, enabled: true, waiting: false)));
    }

    [Fact(DisplayName = "周期性成功点击避免无点击超时")]
    public void GetReason_成功点击更新无点击计时()
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);
        monitor.RecordCallback(TimeSpan.FromSeconds(119));
        monitor.FeedAction("任务节点", "Click", 10, 10, TimeSpan.FromSeconds(119));

        var reason = monitor.GetReason(Timeout, Timeout, enabled: true, waiting: false);

        Assert.Null(reason);
    }

    [Fact(DisplayName = "重复点击达到二次确认阈值后判定画面冻结")]
    public void GetReason_重复点击循环达到阈值时触发游戏恢复()
    {
        var monitor = new TaskRecoveryMonitor();
        monitor.Start(TimeSpan.Zero);
        var now = TimeSpan.Zero;

        for (var i = 0; i < 98; i++)
        {
            now += TimeSpan.FromMilliseconds(100);
            monitor.RecordCallback(now);
            monitor.FeedAction("固定节点", "Click", i, 0, now);
        }

        Assert.Null(monitor.GetReason(now, Timeout, enabled: true, waiting: false));

        now += TimeSpan.FromMilliseconds(100);
        monitor.FeedAction("固定节点", "Click", 999, 0, now);
        var reason = monitor.GetReason(now, Timeout, enabled: true, waiting: false);

        Assert.StartsWith("画面冻结", reason);
        Assert.False(TaskRecoveryMonitor.IsEmulatorUnresponsiveReason(reason));
    }
}
