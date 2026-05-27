using NUnit.Framework;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D8 토스트 큐 — dedup(현재/뒷자리) + cap 정책 검증.
    /// 실제 토스트 visual은 MonoBehaviour + DOTween 필요 → 여긴 큐 상태머신만.
    /// </summary>
    [TestFixture]
    public class ToastQueueTests
    {
        ToastQueue _q;

        [SetUp]
        public void SetUp() => _q = new ToastQueue(maxPending: 3);

        static ToastRequest R(string title, string msg, float dur = 2f)
            => new ToastRequest(title, msg, dur);

        [Test]
        public void EnqueueSingle_Accepted_PendingCountOne()
        {
            Assert.IsTrue(_q.TryEnqueue(R("A", "1")));
            Assert.AreEqual(1, _q.PendingCount);
            Assert.IsFalse(_q.HasCurrent, "TryDequeueNext 호출 전엔 HasCurrent=false");
        }

        [Test]
        public void DequeueNext_FromEmpty_ReturnsFalse()
        {
            Assert.IsFalse(_q.TryDequeueNext(out _));
            Assert.IsFalse(_q.HasCurrent);
        }

        [Test]
        public void DequeueNext_AfterEnqueue_ReturnsItemAndSetsCurrent()
        {
            _q.TryEnqueue(R("A", "1"));
            Assert.IsTrue(_q.TryDequeueNext(out var r));
            Assert.AreEqual("A", r.Title);
            Assert.AreEqual("1", r.Message);
            Assert.IsTrue(_q.HasCurrent);
            Assert.AreEqual(0, _q.PendingCount);
        }

        [Test]
        public void Enqueue_SameAsCurrent_DroppedAsDuplicate()
        {
            _q.TryEnqueue(R("A", "1"));
            _q.TryDequeueNext(out _); // current = A/1
            Assert.IsFalse(_q.TryEnqueue(R("A", "1")), "현재 재생 중인 토스트와 동일 → 드롭");
            Assert.AreEqual(0, _q.PendingCount);
        }

        [Test]
        public void Enqueue_SameAsLastQueued_DroppedAsDuplicate()
        {
            _q.TryEnqueue(R("A", "1"));
            Assert.IsFalse(_q.TryEnqueue(R("A", "1")), "큐 마지막과 동일 → 드롭");
            Assert.AreEqual(1, _q.PendingCount);
        }

        [Test]
        public void Enqueue_DifferentMessage_Accepted()
        {
            _q.TryEnqueue(R("A", "1"));
            Assert.IsTrue(_q.TryEnqueue(R("A", "2")));
            Assert.AreEqual(2, _q.PendingCount);
        }

        [Test]
        public void Enqueue_OverCap_Dropped()
        {
            // 캡 3
            Assert.IsTrue(_q.TryEnqueue(R("A", "1")));
            Assert.IsTrue(_q.TryEnqueue(R("A", "2")));
            Assert.IsTrue(_q.TryEnqueue(R("A", "3")));
            Assert.IsFalse(_q.TryEnqueue(R("A", "4")), "캡 초과 → 새 요청 드롭 (기존 큐 보호)");
            Assert.AreEqual(3, _q.PendingCount);
        }

        [Test]
        public void FIFO_DequeueOrder()
        {
            _q.TryEnqueue(R("T", "first"));
            _q.TryEnqueue(R("T", "second"));
            _q.TryEnqueue(R("T", "third"));

            _q.TryDequeueNext(out var a);
            _q.TryDequeueNext(out var b);
            _q.TryDequeueNext(out var c);

            Assert.AreEqual("first",  a.Message);
            Assert.AreEqual("second", b.Message);
            Assert.AreEqual("third",  c.Message);
        }

        [Test]
        public void MarkCurrentFinished_ClearsHasCurrent()
        {
            _q.TryEnqueue(R("A", "1"));
            _q.TryDequeueNext(out _);
            Assert.IsTrue(_q.HasCurrent);
            _q.MarkCurrentFinished();
            Assert.IsFalse(_q.HasCurrent);
            // 그리고 다시 동일 요청 enqueue 가능
            Assert.IsTrue(_q.TryEnqueue(R("A", "1")));
        }

        [Test]
        public void Clear_EmptiesEverything()
        {
            _q.TryEnqueue(R("A", "1"));
            _q.TryEnqueue(R("A", "2"));
            _q.TryDequeueNext(out _);
            _q.Clear();
            Assert.AreEqual(0, _q.PendingCount);
            Assert.IsFalse(_q.HasCurrent);
        }

        [Test]
        public void ToastRequest_Equals_IgnoresDuration()
        {
            // 같은 텍스트가 살짝 다른 duration으로 와도 dedup
            _q.TryEnqueue(R("A", "1", 1.5f));
            Assert.IsFalse(_q.TryEnqueue(R("A", "1", 3.0f)), "Duration 차이는 dedup 키에서 제외");
        }

        [Test]
        public void Constructor_MaxPendingZero_ClampedToOne()
        {
            var q = new ToastQueue(maxPending: 0);
            Assert.AreEqual(1, q.MaxPending, "0 이하는 1로 클램프");
        }
    }
}
