using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Common;                  // EventBus
using LoveAlgo.Core;                    // GameStateSO
using LoveAlgo.Events;                  // ShowLockScreenCommand, SubmitPasswordCommand, LockMode, CompletionHandle, PasswordVerifyFailedEvent, PasswordAcceptedEvent
using LoveAlgo.Story.StoryEngine.Flow;  // LockScreenController

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// LockScreenController Normal 검증(EditMode). 헤드리스라 OnShow/OnSubmit을 직접 호출하고
    /// 결과 이벤트는 테스트가 구독해 관찰한다. 일치→Accepted+핸들완료, 불일치→Failed(누적)+핸들유지.
    /// </summary>
    public class LockScreenControllerEditModeTests
    {
        GameStateSO _gs;
        LockScreenController _ctrl;
        GameObject _go;
        readonly List<IDisposable> _subs = new();

        [SetUp]
        public void SetUp()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();
            _go = new GameObject("LockScreenController");
            _ctrl = _go.AddComponent<LockScreenController>();
            _ctrl.State = _gs;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        [Test]
        public void Normal_Match_Publishes_Accepted_And_Completes_Handle()
        {
            _gs.Password = "1234";
            bool accepted = false;
            _subs.Add(EventBus.Subscribe<PasswordAcceptedEvent>(_ => accepted = true));
            var handle = new CompletionHandle();
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, handle));

            _ctrl.OnSubmit(new SubmitPasswordCommand("1234"));

            Assert.IsTrue(accepted, "일치 → PasswordAcceptedEvent 발행");
            Assert.IsTrue(handle.IsComplete, "일치 → 핸들 완료(로그인 진행)");
        }

        [Test]
        public void Normal_Mismatch_Publishes_Failed_And_Keeps_Handle()
        {
            _gs.Password = "1234";
            int failCount = 0;
            int lastErr = 0;
            _subs.Add(EventBus.Subscribe<PasswordVerifyFailedEvent>(e => { failCount++; lastErr = e.ErrorCount; }));
            var handle = new CompletionHandle();
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, handle));

            _ctrl.OnSubmit(new SubmitPasswordCommand("9999"));
            Assert.AreEqual(1, failCount, "불일치 1회 → Failed 1회");
            Assert.AreEqual(1, lastErr, "누적 오류 = 1");
            Assert.IsFalse(handle.IsComplete, "불일치 → 핸들 유지(재입력)");

            _ctrl.OnSubmit(new SubmitPasswordCommand("8888"));
            Assert.AreEqual(2, failCount, "불일치 2회 → Failed 2회");
            Assert.AreEqual(2, lastErr, "누적 오류 = 2");
            Assert.IsFalse(handle.IsComplete);
        }

        [Test]
        public void Normal_ErrorCount_Resets_On_New_Show()
        {
            _gs.Password = "1234";
            int lastErr = 0;
            _subs.Add(EventBus.Subscribe<PasswordVerifyFailedEvent>(e => lastErr = e.ErrorCount));
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            _ctrl.OnSubmit(new SubmitPasswordCommand("x"));
            _ctrl.OnSubmit(new SubmitPasswordCommand("y"));
            Assert.AreEqual(2, lastErr, "리셋 전 누적 2");

            // 새 잠금화면(Show) → 오류 횟수 리셋
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            _ctrl.OnSubmit(new SubmitPasswordCommand("z"));
            Assert.AreEqual(1, lastErr, "새 Show 후 누적 1로 리셋");
        }

        [Test]
        public void ResetRequest_Switches_To_Reset_And_Saves_New_Password()
        {
            _gs.Password = "1234";
            var handle = new CompletionHandle();
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, handle));
            _ctrl.OnSubmit(new SubmitPasswordCommand("9999")); // 불일치 — 유지
            Assert.IsFalse(handle.IsComplete, "불일치 후 유지");

            _ctrl.OnResetRequested(); // 재설정 진입(핸들 유지, 모드 Reset)

            _ctrl.OnSubmit(new SubmitPasswordCommand("5555")); // Reset 저장
            Assert.AreEqual("5555", _gs.Password, "Reset → 새 비번 저장");
            Assert.IsTrue(handle.IsComplete, "Reset 저장 후 핸들 완료(진행)");
        }
    }
}
