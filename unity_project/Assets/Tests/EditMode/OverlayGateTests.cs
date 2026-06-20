using System;
using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>OverlayGate 스택(토큰 발급/해제·차단·뒤로가기 CloseTop·Enter ConfirmTop·비-LIFO 해제·리셋) 단위테스트.</summary>
    public class OverlayGateTests
    {
        [SetUp] public void SetUp() => OverlayGate.Reset();
        [TearDown] public void TearDown() => OverlayGate.Reset();

        [Test]
        public void PushDispose_TogglesBlocked_Counted()
        {
            Assert.IsFalse(OverlayGate.IsBlocked);
            var a = OverlayGate.Push();
            Assert.IsTrue(OverlayGate.IsBlocked);
            var b = OverlayGate.Push(); // 다중 오버레이
            a.Dispose();
            Assert.IsTrue(OverlayGate.IsBlocked, "1개 남음 → 여전히 차단");
            b.Dispose();
            Assert.IsFalse(OverlayGate.IsBlocked);
        }

        [Test]
        public void Dispose_Twice_Safe()
        {
            var a = OverlayGate.Push();
            var b = OverlayGate.Push();
            a.Dispose();
            a.Dispose(); // 중복 해제 — b가 같이 빠지면 안 됨
            Assert.AreEqual(1, OverlayGate.Count);
            b.Dispose();
            Assert.IsFalse(OverlayGate.IsBlocked);
        }

        [Test]
        public void CloseTop_InvokesLastPushed_LifoOrder()
        {
            int closedA = 0, closedB = 0;
            IDisposable ta = null, tb = null;
            ta = OverlayGate.Push(() => { closedA++; ta.Dispose(); });
            tb = OverlayGate.Push(() => { closedB++; tb.Dispose(); });

            Assert.IsTrue(OverlayGate.CloseTop(), "최상단 B 닫기 요청");
            Assert.AreEqual(0, closedA);
            Assert.AreEqual(1, closedB, "마지막에 출력된 팝업부터(기획서 규칙)");

            Assert.IsTrue(OverlayGate.CloseTop(), "다음 최상단 A");
            Assert.AreEqual(1, closedA);
            Assert.IsFalse(OverlayGate.IsBlocked);
        }

        [Test]
        public void CloseTop_AfterOutOfOrderDispose_TargetsRemainingTop()
        {
            int closedB = 0;
            var ta = OverlayGate.Push(() => Assert.Fail("A는 이미 해제됨 — 호출되면 안 됨"));
            IDisposable tb = null;
            tb = OverlayGate.Push(() => { closedB++; tb.Dispose(); });
            ta.Dispose(); // 비-LIFO: 아래 깔린 A가 먼저 강제 종료(OnDisable 등)

            Assert.IsTrue(OverlayGate.CloseTop());
            Assert.AreEqual(1, closedB, "남은 최상단 B가 닫힘(자기 항목만 정확히 제거됐다는 증거)");
            Assert.IsFalse(OverlayGate.IsBlocked);
        }

        [Test]
        public void CloseTop_TopWithoutCloseAction_ReturnsFalse_NoSkip()
        {
            int closedA = 0;
            IDisposable ta = null;
            ta = OverlayGate.Push(() => { closedA++; ta.Dispose(); });
            OverlayGate.Push(); // 닫기 불가 오버레이가 최상단(차단 전용)

            Assert.IsFalse(OverlayGate.CloseTop(), "최상단이 닫기 불가면 false");
            Assert.AreEqual(0, closedA, "아래(가려진) 팝업을 건너 닫지 않는다");
            Assert.IsTrue(OverlayGate.IsBlocked);
        }

        [Test]
        public void CloseTop_Empty_ReturnsFalse()
        {
            Assert.IsFalse(OverlayGate.CloseTop());
        }

        [Test]
        public void ConfirmTop_InvokesTopConfirmAction()
        {
            int confirmedA = 0, confirmedB = 0;
            OverlayGate.Push(() => { }, () => confirmedA++);
            OverlayGate.Push(() => { }, () => confirmedB++);

            Assert.IsTrue(OverlayGate.ConfirmTop(), "최상단 B 확정");
            Assert.AreEqual(0, confirmedA, "아래(가려진) 팝업은 확정 안 됨");
            Assert.AreEqual(1, confirmedB, "최상단만 확정");
        }

        [Test]
        public void ConfirmTop_TopWithoutConfirmAction_ReturnsFalse_NoSkip()
        {
            int confirmedA = 0;
            OverlayGate.Push(() => { }, () => confirmedA++);
            OverlayGate.Push(() => { }); // 확인 액션 없는 오버레이(설정/세이브로드)가 최상단

            Assert.IsFalse(OverlayGate.ConfirmTop(), "최상단이 확인 불가면 false(Enter 무동작)");
            Assert.AreEqual(0, confirmedA, "아래 팝업을 건너 확정하지 않는다");
        }

        [Test]
        public void ConfirmTop_Empty_ReturnsFalse()
        {
            Assert.IsFalse(OverlayGate.ConfirmTop());
        }

        [Test]
        public void Push_LegacySingleArg_StillWorks_NoConfirm()
        {
            // 기존 호출부(Push(닫기)만)는 그대로 — Enter는 무동작이어야 한다.
            int closed = 0;
            IDisposable t = null;
            t = OverlayGate.Push(() => { closed++; t.Dispose(); });
            Assert.IsFalse(OverlayGate.ConfirmTop(), "확인 액션 없음 → Enter 무동작");
            Assert.IsTrue(OverlayGate.CloseTop(), "ESC는 정상 닫힘");
            Assert.AreEqual(1, closed);
        }

        [Test]
        public void Reset_ClearsAll_LateDisposeSafe()
        {
            var a = OverlayGate.Push(() => { });
            OverlayGate.Push(() => { });
            OverlayGate.Reset();
            Assert.IsFalse(OverlayGate.IsBlocked);
            a.Dispose(); // 리셋 뒤 늦은 해제 — 무해해야 함
            Assert.AreEqual(0, OverlayGate.Count);
        }
    }
}
