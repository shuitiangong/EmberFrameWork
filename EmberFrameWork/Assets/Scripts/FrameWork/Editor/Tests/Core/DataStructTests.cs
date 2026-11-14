using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class DataStructTests
    {
        [Test]
        public void TypeNamePairSeparatesPoolsAndNormalizesNullName()
        {
            var pairs = new Dictionary<TypeNamePair, int>
            {
                [new TypeNamePair(typeof(int), "assets")] = 1,
                [new TypeNamePair(typeof(int), "ui")] = 2,
                [new TypeNamePair(typeof(string), "assets")] = 3
            };
            Assert.AreEqual(3, pairs.Count);
            Assert.AreEqual(1, pairs[new TypeNamePair(typeof(int), "assets")]);
            Assert.AreEqual(new TypeNamePair(typeof(int)), new TypeNamePair(typeof(int), null));
            pairs[default] = 4;
            Assert.AreEqual(4, pairs[default]);
            Assert.Throws<GameFrameworkException>(() => new TypeNamePair(null));
        }

        [Test]
        public void LinkedListReusesRemovedNodesAndClearsReferences()
        {
            var list = new GameFrameworkLinkedList<object>();
            var node = list.AddLast(new object());
            list.Remove(node);
            Assert.IsNull(node.Value);
            Assert.IsNull(node.List);
            Assert.AreEqual(1, list.CachedNodeCount);
            Assert.AreSame(node, list.AddFirst(new object()));
            Assert.AreEqual(0, list.CachedNodeCount);
            list.AddLast(new object());
            list.Clear();
            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(2, list.CachedNodeCount);
            Assert.IsNull(node.Value);
            list.AddLast("new");
            CollectionAssert.AreEqual(new[] { "new" }, list);
            list.ClearCachedNodes();
            Assert.AreEqual(0, list.CachedNodeCount);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void RangeExcludesTerminalAndSupportsNullValues()
        {
            var list = new LinkedList<string>();
            var first = list.AddLast((string)null);
            list.AddLast("asset");
            var terminal = list.AddLast("terminal");
            var range = new GameFrameworkLinkedListRange<string>(first, terminal);
            Assert.AreEqual(2, range.Count);
            Assert.IsTrue(range.Contains(null));
            Assert.IsFalse(range.Contains("terminal"));
            CollectionAssert.AreEqual(new string[] { null, "asset" }, range);
            Assert.IsFalse(default(GameFrameworkLinkedListRange<string>).IsValid);
        }

        [Test]
        public void MultiDictionaryRemovesFirstMiddleAndLastWithoutTouchingOtherKeys()
        {
            var map = new GameFrameworkMultiDictionary<string, string>();
            map.Add("a", null);
            map.Add("b", "other");
            map.Add("a", "middle");
            map.Add("a", "last");
            Assert.IsTrue(map.Contains("a", null));
            Assert.IsTrue(map.Remove("a", "middle"));
            Assert.IsTrue(map.Remove("a", null));
            CollectionAssert.AreEqual(new[] { "last" }, map["a"]);
            Assert.IsTrue(map.Remove("a", "last"));
            Assert.IsFalse(map.Contains("a"));
            Assert.AreEqual(1, map.Count);
            CollectionAssert.AreEqual(new[] { "other" }, map["b"]);
        }

        [Test]
        public void MultiDictionarySupportsDuplicatesRemoveAllAndReuseAfterClear()
        {
            var map = new GameFrameworkMultiDictionary<int, int>();
            map.Add(1, 10);
            map.Add(2, 20);
            map.Add(1, 10);
            Assert.IsTrue(map.Remove(1, 10));
            Assert.AreEqual(1, map[1].Count);
            Assert.IsTrue(map.RemoveAll(1));
            CollectionAssert.AreEqual(new[] { 20 }, map[2]);
            Assert.IsFalse(map.TryGetValue(1, out var missing));
            Assert.IsFalse(missing.IsValid);
            Assert.IsFalse(map.RemoveAll(1));
            map.Clear();
            map.Add(3, 30);
            Assert.AreEqual(1, map.Count);
            CollectionAssert.AreEqual(new[] { 30 }, map[3]);
        }

        [Test]
        public void LinkedListEnumeratorResetReturnsToFirstElement()
        {
            var list = new GameFrameworkLinkedList<int>();
            list.AddLast(1);
            list.AddLast(2);
            IEnumerator iterator = ((IEnumerable)list).GetEnumerator();
            Assert.IsTrue(iterator.MoveNext());
            Assert.IsTrue(iterator.MoveNext());
            iterator.Reset();
            Assert.IsTrue(iterator.MoveNext());
            Assert.AreEqual(1, iterator.Current);
        }

        [Test]
        public void DictionaryEnumeratorResetWorksAfterEnd()
        {
            var map = new GameFrameworkMultiDictionary<int, int>();
            map.Add(1, 10);
            IEnumerator iterator = ((IEnumerable)map).GetEnumerator();
            Assert.IsTrue(iterator.MoveNext());
            Assert.IsFalse(iterator.MoveNext());
            iterator.Reset();
            Assert.IsTrue(iterator.MoveNext());
            Assert.AreEqual(1, ((KeyValuePair<int, GameFrameworkLinkedListRange<int>>)iterator.Current).Key);
        }
    }
}
