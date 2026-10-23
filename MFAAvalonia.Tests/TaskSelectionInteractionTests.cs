using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MFAAvalonia.Extensions;
using Xunit;

namespace MFAAvalonia.Tests;

[Collection("策略界面")]
public sealed class TaskSelectionInteractionTests
{
    [Theory(DisplayName = "任务勾选禁用时左右键均不能改变三态选择")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public void DisabledCheckBox_PreservesSelection(bool? initial, bool rightButton)
    {
        var checkBox = new CheckBox { IsChecked = initial, IsEnabled = false };
        CheckBoxExtensions.SetEnableRightClickNull(checkBox, true);

        Press(checkBox, rightButton);

        Assert.Equal(initial, checkBox.IsChecked);
    }

    [Theory(DisplayName = "上级控件禁用时任务勾选也不能改变")]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledContainer_PreservesSelection(bool rightButton)
    {
        var checkBox = new CheckBox { IsChecked = true };
        var container = new Border { Child = checkBox, IsEnabled = false };
        CheckBoxExtensions.SetEnableRightClickNull(checkBox, true);

        Press(checkBox, rightButton);

        Assert.False(checkBox.IsEffectivelyEnabled);
        Assert.True(checkBox.IsChecked);
    }

    [Theory(DisplayName = "恢复可用后任务勾选仍支持左右键三态切换")]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(null, false, false)]
    [InlineData(true, true, null)]
    [InlineData(false, true, null)]
    [InlineData(null, true, false)]
    public void ReenabledCheckBox_ChangesSelection(bool? initial, bool rightButton, bool? expected)
    {
        var checkBox = new CheckBox { IsChecked = initial, IsEnabled = false };
        CheckBoxExtensions.SetEnableRightClickNull(checkBox, true);
        checkBox.IsEnabled = true;

        Press(checkBox, rightButton);

        Assert.Equal(expected, checkBox.IsChecked);
    }

    private static void Press(CheckBox checkBox, bool rightButton)
    {
        var pointer = new Pointer(1, PointerType.Mouse, true);
        var properties = new PointerPointProperties(
            rightButton ? RawInputModifiers.RightMouseButton : RawInputModifiers.LeftMouseButton,
            rightButton ? PointerUpdateKind.RightButtonPressed : PointerUpdateKind.LeftButtonPressed);
        checkBox.RaiseEvent(new PointerPressedEventArgs(checkBox, pointer, checkBox,
            new Point(1, 1), 0, properties, KeyModifiers.None, 1));
    }
}
