using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MFAAvalonia.Features.CombatStrategies.Controls;
using System.Reflection;
using Xunit;

namespace MFAAvalonia.Tests;

[Collection("策略界面")]
public sealed class CombatStrategyReorderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedDrag_RestoresRowsWithoutLeavingPreviewOffsets(bool cancel)
    {
        var list = new StackPanel { Spacing = 8 };
        var handles = Enumerable.Range(0, 3).Select(_ => new Border { Width = 30, Height = 40 }).ToArray();
        var rows = handles.Select(handle => new Border { Height = 40, Child = handle }).ToArray();
        var values = new List<int> { 1, 2, 3 };
        var changes = 0;
        var reorder = typeof(StrategyStageCard).Assembly.GetType("MFAAvalonia.Features.CombatStrategies.Controls.StrategyReorder")!;
        var attach = reorder.GetMethod("Attach", BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(int));
        foreach (var (row, index) in rows.Select((row, index) => (row, index)))
        {
            list.Children.Add(row);
            attach.Invoke(null, [handles[index], row, list, values, index + 1, (Action)(() => changes++), Avalonia.Layout.Orientation.Vertical]);
        }
        list.Measure(new Size(300, 300));
        list.Arrange(new Rect(0, 0, 300, 300));

        // 排序结束后列表会保留归零的动画变换，下一次拖动不能修改这些待恢复的对象。
        var previousTransforms = rows.Select(_ => new TranslateTransform()).ToArray();
        for (var i = 0; i < rows.Length; i++) rows[i].RenderTransform = previousTransforms[i];
        var pointer = new Avalonia.Input.Pointer(1, PointerType.Mouse, true);
        var pressed = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var press = new PointerPressedEventArgs(handles[0], pointer, list, new Point(10, 10), 0, pressed, KeyModifiers.None, 1);
        Assert.True(press.GetCurrentPoint(handles[0]).Properties.IsLeftButtonPressed);
        handles[0].RaiseEvent(press);
        Assert.Same(handles[0], pointer.Captured);
        var move = new PointerEventArgs(InputElement.PointerMovedEvent, handles[0], pointer, list,
            new Point(10, 75), 1, pressed, KeyModifiers.None);
        Assert.Equal(new Point(10, 75), move.GetPosition(list));
        handles[0].RaiseEvent(move);
        Assert.Contains("strategy-dragging", rows[0].Classes);
        Assert.NotEqual(0, rows[0].RenderTransform!.Value.M32);

        if (cancel)
            pointer.Capture(null);
        else
            handles[0].RaiseEvent(new PointerReleasedEventArgs(handles[0], pointer, list, new Point(10, 10), 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));

        Assert.Equal(new[] { 1, 2, 3 }, values);
        Assert.Equal(0, changes);
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(Matrix.Identity, rows[i].RenderTransform!.Value);
            Assert.Same(previousTransforms[i], rows[i].RenderTransform);
        }
    }
}
