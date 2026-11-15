using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    /// <summary>以 UnityTest 协程入口等待现有 Task 测试，不改变测试中的断言。</summary>
    internal static class ResourceTestTask
    {
        public static IEnumerator Wait(Task task)
        {
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (timer.Elapsed.TotalSeconds > 20) Assert.Fail("异步资源测试超时。");
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }
    }
}
