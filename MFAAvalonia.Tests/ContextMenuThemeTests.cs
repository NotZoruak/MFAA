using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MFAAvalonia.Controls;
using SukiUI;
using Xunit;

namespace MFAAvalonia.Tests;

[Collection("策略界面")]
public sealed class ContextMenuThemeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextMenu_UsesApplicationThemeAndRendersItems(bool lifecycleSafe)
    {
        ContextMenu menu = lifecycleSafe ? new LifecycleSafeContextMenu() : new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "重命名" });
        menu.Items.Add(new MenuItem { Header = "删除" });
        var host = new Border { Child = menu };
        host.Styles.Add(new SukiTheme());

        // 加载实际主题并完成布局，确认菜单内容能够绘制，而不只检查打开事件。
        host.Measure(new Size(600, 400));
        host.Arrange(new Rect(0, 0, 600, 400));

        Assert.NotNull(menu.Template);
        Assert.True(menu.DesiredSize.Width > 0);
        Assert.True(menu.DesiredSize.Height > 0);
        var presenters = menu.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().ToArray();
        Assert.Contains(presenters, x => Equals(x.Content, "重命名"));
        Assert.Contains(presenters, x => Equals(x.Content, "删除"));
        Assert.All(menu.GetVisualDescendants().OfType<MenuItem>(), item => Assert.True(item.Bounds.Height > 0));
    }
}
