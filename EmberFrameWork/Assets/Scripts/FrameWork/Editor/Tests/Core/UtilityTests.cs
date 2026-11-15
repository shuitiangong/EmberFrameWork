using System;
using System.Threading;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class UtilityTests
    {
        [SetUp]
        public void SetUp() => Utility.Text.SetTextHelper(new DefaultTextHelper());

        [TearDown]
        public void TearDown() => Utility.Text.SetTextHelper(null);

        [Test]
        public void TextHelperAndFallbackFormatArguments()
        {
            Assert.AreEqual("value=7/8", Utility.Text.Format("value={0}/{1}", 7, 8));
            Utility.Text.SetTextHelper(null);
            Assert.AreEqual("value=7/8", Utility.Text.Format("value={0}/{1}", 7, 8));
        }

        [Test]
        public void FormatExceptionDoesNotPoisonNextCall()
        {
            Assert.Throws<FormatException>(() => Utility.Text.Format("bad {9}", 1));
            Assert.AreEqual("next=2", Utility.Text.Format("next={0}", 2));
            Utility.Text.SetTextHelper(null);
            Assert.AreEqual("fallback=3", Utility.Text.Format("fallback={0}", 3));
        }

        [Test]
        public void FormattingBuffersAreIsolatedBetweenThreads()
        {
            Exception failure = null;
            var threads = new Thread[4];
            for (int i = 0; i < threads.Length; i++)
            {
                int id = i;
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        for (int n = 0; n < 100; n++)
                            Assert.AreEqual($"{id}:{n}", Utility.Text.Format("{0}:{1}", id, n));
                    }
                    catch (Exception exception) { Interlocked.CompareExchange(ref failure, exception, null); }
                });
                threads[i].Start();
            }
            foreach (var thread in threads) thread.Join();
            if (failure != null) throw failure;
        }

    }
}
