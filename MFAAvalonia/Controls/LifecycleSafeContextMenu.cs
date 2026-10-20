using Avalonia;
using Avalonia.Controls;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Controls;

public sealed class LifecycleSafeContextMenu : ContextMenu
{
    // 只扩展卸载保护，仍使用标准菜单的主题和绘制模板。
    protected override Type StyleKeyOverride => typeof(ContextMenu);

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        try
        {
            base.OnDetachedFromVisualTree(e);
        }
        catch (NotSupportedException ex) when (ex.Message == "DefaultMenuInteractionHandler is not attached to the menu.")
        {
            LoggerHelper.Warn("关闭窗口时忽略 Avalonia 对未注册右键菜单的重复卸载请求。");
        }
    }
}
