using System;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Helper;

/// <summary>管理一次停止操作与交互状态。</summary>
public sealed class TaskStopLifecycle
{
    private int _isStopping;

    public bool IsStopping => Volatile.Read(ref _isStopping) != 0;

    public async Task RunAsync(Func<Task> stop, Action<bool> setStopping)
    {
        if (Interlocked.CompareExchange(ref _isStopping, 1, 0) != 0)
            return;

        try
        {
            setStopping(true);
            await stop();
        }
        finally
        {
            try
            {
                // 提前返回、取消和异步异常都必须走到同一收尾入口。
                setStopping(false);
            }
            finally
            {
                Interlocked.Exchange(ref _isStopping, 0);
            }
        }
    }
}
