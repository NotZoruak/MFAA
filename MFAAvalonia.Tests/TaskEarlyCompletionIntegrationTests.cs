using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper.ValueType;
using SkiaSharp;
using Xunit;

namespace MFAAvalonia.Tests;

public sealed class TaskEarlyCompletionIntegrationTests
{
    [Fact(DisplayName = "原生循环中请求结束后执行器可继续后续任务")]
    public async Task Run_原生循环退出后仍可执行下一任务()
    {
        var scratchRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "maa-completion-tests"));
        var directory = Path.Combine(scratchRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "pipeline"));
        byte[] frame;
        using (var image = new SKBitmap(16, 16))
        using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100))
            frame = encoded.ToArray();
        File.WriteAllText(Path.Combine(directory, "pipeline", "completion.json"), """
            {
              "Loop": {
                "next": ["[JumpBack]NoRecovery"]
              },
              "NoRecovery": {
                "action": { "type": "Custom", "param": { "custom_action": "RequestCompletion" } },
                "pre_delay": 0,
                "post_delay": 0
              },
              "Next": {}
            }
            """);

        try
        {
            using var resource = new MaaResource();
            Assert.Equal(MaaJobStatus.Succeeded, resource.AppendBundle(directory).Wait());
            using var controller = new MaaCustomController(new ImageController(frame));
            using var tasker = new MaaTasker(controller, resource, DisposeOptions.None);
            Assert.True(tasker.IsInitialized);
            var task = new MFATask
            {
                Name = "原生循环任务",
                Count = 30,
                MaaAction = () =>
                {
                    var job = tasker.AppendTask("Loop", "{}");
                    return Task.Run(() => job.Wait());
                },
                StopCurrentIterationAsync = () => Task.Run(() => tasker.Stop().Wait())
            };
            var completionAction = new RequestCompletionAction(task);
            Assert.True(resource.Register(completionAction));

            try
            {
                var result = await task.Run(TestContext.Current.CancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                Assert.Equal(MFATask.MFATaskStatus.SUCCEEDED, result.Status);
                Assert.True(completionAction.Calls >= 1);
                Assert.Equal(MaaJobStatus.Succeeded, tasker.AppendTask("Next", "{}").Wait());
            }
            finally
            {
                await Task.Run(() => tasker.Stop().Wait());
            }
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(directory)) != scratchRoot)
                throw new InvalidOperationException("测试暂存目录越界，停止清理。");
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RequestCompletionAction(MFATask task) : IMaaCustomAction
    {
        public string Name { get; set; } = "RequestCompletion";
        public int Calls;

        public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
        {
            Interlocked.Increment(ref Calls);
            return task.RequestEarlyCompletion("无可用体力回复道具");
        }
    }

    private sealed class ImageController(byte[] frame) : IMaaCustomController
    {
        public string Name { get; set; } = "离线图片控制器";
        public bool Connect() => true;
        public bool Connected() => true;
        public bool RequestUuid(IMaaStringBuffer buffer) => buffer.TrySetValue("completion-test", true);
        public ControllerFeatures GetFeatures() => ControllerFeatures.None;
        public bool Screencap(IMaaImageBuffer buffer) => buffer.TrySetEncodedData(frame);
        public bool Inactive() => true;
        public bool GetInfo(IMaaStringBuffer buffer) => buffer.TrySetValue("{}", true);

        // 此测试只允许读取图片，意外输入必须失败。
        public bool StartApp(string intent) => false;
        public bool StopApp(string intent) => false;
        public bool Click(int x, int y) => false;
        public bool Swipe(int x1, int y1, int x2, int y2, int duration) => false;
        public bool TouchDown(int contact, int x, int y, int pressure) => false;
        public bool TouchMove(int contact, int x, int y, int pressure) => false;
        public bool TouchUp(int contact) => false;
        public bool ClickKey(int key) => false;
        public bool InputText(string text) => false;
        public bool KeyDown(int key) => false;
        public bool KeyUp(int key) => false;
        public bool Scroll(int dx, int dy) => false;
        public bool RelativeMove(int dx, int dy) => false;
        public bool Shell(string command, long timeout, IMaaStringBuffer buffer) => false;
        public void Dispose() { }
    }
}
