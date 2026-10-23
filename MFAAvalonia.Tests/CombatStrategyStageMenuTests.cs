using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia;
using Avalonia.Input;
using MFAAvalonia.Features.CombatStrategies;
using MFAAvalonia.Features.CombatStrategies.Controls;
using Xunit;

namespace MFAAvalonia.Tests;

[Collection("策略界面")]
public sealed class CombatStrategyStageMenuTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StageHeader_ContextRequestOpensMenu(bool expanded)
    {
        var session = new StrategyEditorSession(new StrategyStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        session.Create("阶段菜单");
        var card = new StrategyStageCard(session, session.Current.Stages[0], _ => { }, (_, _) => Task.FromResult<string?>(null));
        card.IsExpanded = expanded;
        var owner = card.GetLogicalDescendants().OfType<Control>().Single(x => x.ContextMenu != null);
        var title = owner.GetLogicalDescendants().OfType<Button>().Single();
        var opened = false;
        owner.ContextMenu!.Opening += (_, args) => { opened = true; args.Cancel = true; };
        title.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        Assert.True(opened);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StageHeader_RightClickOpensMenu(bool useTitle, bool expanded)
    {
        var session = new StrategyEditorSession(new StrategyStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        session.Create("阶段菜单");
        var card = new StrategyStageCard(session, session.Current.Stages[0], _ => { }, (_, _) => Task.FromResult<string?>(null));
        card.IsExpanded = expanded;
        var owner = card.GetLogicalDescendants().OfType<Control>().Single(x => x.ContextMenu != null);
        var header = card.GetLogicalDescendants().OfType<Border>()
            .Single(x => x.Child is Grid grid && grid.Children.Contains(card.DragHandle));
        var target = useTitle ? owner.GetLogicalDescendants().OfType<Button>().Single() : (Control)header;
        var opened = false;
        owner.ContextMenu!.Opening += (_, args) => { opened = true; args.Cancel = true; };
        card.Measure(new Size(700, double.PositiveInfinity));
        card.Arrange(new Rect(0, 0, 700, card.DesiredSize.Height));
        var pointer = new Avalonia.Input.Pointer(1, PointerType.Mouse, true);
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, card, new Point(30, 20), 0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None, 1));
        target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, card, new Point(30, 20), 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.RightButtonReleased), KeyModifiers.None, MouseButton.Right));
        Assert.True(opened);
    }
}
