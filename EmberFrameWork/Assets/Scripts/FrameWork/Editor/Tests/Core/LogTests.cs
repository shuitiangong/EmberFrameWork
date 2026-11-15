using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class LogTests
    {
        private sealed class CaptureHelper : GameFrameworkLog.ILogHelper
        {
            public readonly List<GameFrameworkLogLevel> Levels = new List<GameFrameworkLogLevel>();
            public readonly List<object> Messages = new List<object>();
            public void Log(GameFrameworkLogLevel level, object message)
            {
                Levels.Add(level);
                Messages.Add(message);
            }
        }

        private CaptureHelper _capture;

        [SetUp]
        public void SetUp()
        {
            _capture = new CaptureHelper();
            GameFrameworkLog.SetLogHelper(_capture);
        }

        [TearDown]
        public void TearDown() => GameFrameworkLog.SetLogHelper(new DefaultLogHelper());

        [Test]
        public void RoutesAllLevelsToCustomHelper()
        {
            GameFrameworkLog.Debug("debug");
            GameFrameworkLog.Info("info");
            GameFrameworkLog.Warning("warning");
            GameFrameworkLog.Error("error");
            GameFrameworkLog.Fatal("fatal");
            CollectionAssert.AreEqual(new[] { GameFrameworkLogLevel.Debug, GameFrameworkLogLevel.Info,
                GameFrameworkLogLevel.Warning, GameFrameworkLogLevel.Error, GameFrameworkLogLevel.Fatal }, _capture.Levels);
        }

        [Test]
        public void FormatsSingleAndSixteenArguments()
        {
            GameFrameworkLog.Info("asset={0}", "Hero");
            GameFrameworkLog.Info("{0}/{15}", 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
            CollectionAssert.AreEqual(new[] { "asset=Hero", "0/15" }, _capture.Messages);
        }

        [Test]
        public void NullHelperSkipsFormatting()
        {
            GameFrameworkLog.SetLogHelper(null);
            Assert.DoesNotThrow(() => GameFrameworkLog.Info("{invalid}", 1));
            Assert.IsEmpty(_capture.Messages);
        }

        [Test]
        public void FatalExceptionForwardsFormattedText()
        {
            var original = new InvalidOperationException("original");
            Log.Fatal(original);
#if ENABLE_LOG
            Assert.AreEqual(original.ToString(), _capture.Messages[0]);
            Assert.AreEqual(GameFrameworkLogLevel.Fatal, _capture.Levels[0]);
#else
            Assert.IsEmpty(_capture.Messages);
#endif
        }

        [Test]
        public void DefaultFatalPreservesTEngineExceptionBehavior()
        {
            var helper = new DefaultLogHelper();
            Assert.Throws<NullReferenceException>(() => helper.Log(GameFrameworkLogLevel.Fatal, null));
            var original = new Exception("original");
            var thrown = Assert.Throws<Exception>(() => helper.Log(GameFrameworkLogLevel.Fatal, original));
            Assert.IsNull(thrown.InnerException);
            StringAssert.Contains(original.ToString(), thrown.Message);
        }
    }
}
