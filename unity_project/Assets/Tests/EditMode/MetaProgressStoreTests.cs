using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;  // MetaProgressStore, GameStateSO
using LoveAlgo.Story; // ConditionEvaluator

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// MetaProgressStore(PlayerPrefs 메타 영속 — 세이브와 분리, 회차 카운터) 순수 I/O + ConditionEvaluator의
    /// <c>Meta:</c> 아톰 게이트 통합 검증. 메타 키만 건드리고 Set/TearDown에서 청소(에디터 prefs 격리).
    /// </summary>
    public class MetaProgressStoreTests
    {
        const string K = MetaProgressStore.PrologueClears;

        [SetUp] public void SetUp() => MetaProgressStore.DeleteKey(K);
        [TearDown] public void TearDown() => MetaProgressStore.DeleteKey(K);

        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        [Test]
        public void GetInt_Unset_ReturnsDefault()
        {
            Assert.AreEqual(0, MetaProgressStore.GetInt(K), "미설정 키 = 기본 0");
            Assert.AreEqual(7, MetaProgressStore.GetInt(K, 7), "미설정 키 = 지정 기본값");
        }

        [Test]
        public void Increment_AccumulatesAndPersists()
        {
            Assert.AreEqual(1, MetaProgressStore.Increment(K), "첫 증가 → 1");
            Assert.AreEqual(2, MetaProgressStore.Increment(K), "둘째 증가 → 2");
            Assert.AreEqual(2, MetaProgressStore.GetInt(K), "증가값 영속");
            Assert.AreEqual(5, MetaProgressStore.Increment(K, 3), "delta 증가");
        }

        [Test]
        public void SetInt_RoundTrips_And_DeleteResets()
        {
            MetaProgressStore.SetInt(K, 4);
            Assert.AreEqual(4, MetaProgressStore.GetInt(K));
            MetaProgressStore.DeleteKey(K);
            Assert.AreEqual(0, MetaProgressStore.GetInt(K), "삭제 후 기본 0 복귀");
        }

        [Test]
        public void Condition_Meta_GatesByCounter()
        {
            var gs = MakeState(); // Meta: 아톰은 gs를 안 쓰지만 Evaluate의 null 가드 통과용으로 필요.

            // 0회차: 어떤 임계도 미충족.
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Meta:prologueClears>=1"));

            MetaProgressStore.SetInt(K, 1);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Meta:prologueClears>=1"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Meta:prologueClears>=2"));

            MetaProgressStore.SetInt(K, 3);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Meta:prologueClears>=3"));
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Meta:prologueClears>=1"), "상위 충족은 하위도 충족");

            Object.DestroyImmediate(gs);
        }
    }
}
