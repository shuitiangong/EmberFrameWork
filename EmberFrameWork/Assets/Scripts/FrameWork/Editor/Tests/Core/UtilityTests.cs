using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class UtilityTests
    {
        private sealed class NestedText
        {
            public override string ToString() => Utility.Text.Format("inner={0}", 7);
        }

        [SetUp]
        public void SetUp() => Utility.Text.ResetTextHelper();

        [TearDown]
        public void TearDown() => Utility.Text.ResetTextHelper();

        [Test]
        public void NestedFormattingDoesNotOverwriteOuterText()
        {
            Assert.AreEqual("before inner=7 after", Utility.Text.Format("before {0} after", new NestedText()));
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

        [Test]
        public void DownloadSizeHandlesUnitBoundariesAndLargeValues()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.AreEqual("0 Bytes", Utility.File.GetLengthString(0));
                Assert.AreEqual("1023 Bytes", Utility.File.GetLengthString(1023));
                Assert.AreEqual("1.00 KB", Utility.File.GetLengthString(1024));
                Assert.AreEqual("1.00 MB", Utility.File.GetLengthString(1024L * 1024));
                Assert.AreEqual("3.00 GB", Utility.File.GetLengthString(3L * 1024 * 1024 * 1024));
                Assert.Throws<ArgumentOutOfRangeException>(() => Utility.File.GetLengthString(-1));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void NormalizeResourcePathsPreservesNullAndUrls()
        {
            Assert.IsNull(Utility.Path.GetRegularPath(null));
            Assert.AreEqual("Assets/RawAssets/UI", Utility.Path.GetRegularPath(@"Assets\RawAssets\UI"));
            Assert.AreEqual("https://cdn.example/game", Utility.Path.GetRegularPath("https://cdn.example/game"));
        }
    }
}
