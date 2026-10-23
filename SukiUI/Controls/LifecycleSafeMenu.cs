using Avalonia;
using Avalonia.Controls;
using System;

namespace SukiUI.Controls;

public sealed class LifecycleSafeMenu : Menu
{
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        try
        {
            base.OnDetachedFromVisualTree(e);
        }
        catch (NotSupportedException ex) when (ex.Message == "DefaultMenuInteractionHandler is not attached to the menu.")
        {
            // SukiWindow 的隐藏菜单可能未注册到平台处理器，关闭窗口时忽略对应的重复卸载。
        }
    }
}
