using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Schedule; // ScheduleConfirmButton, ScheduleType, ScheduleTable, ScheduleSelectedCommand
using LoveAlgo.UI;       // ModalView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 슬라이스 2(정적 UI): 운동/공부 탭은 탭전환 대신 확인 팝업을 띄우고 "예"일 때만 스케줄을 실행한다.
    /// 실 씬에서 운동 탭(Exercise_A)의 <see cref="ScheduleConfirmButton"/>을 눌러 ModalView가 효과 요약과 함께
    /// 뜨고, "예"(index 1) 클릭 시 <see cref="ScheduleSelectedCommand"/>가 발행되는지 검증한다.
    /// </summary>
    public class ScheduleConfirmPlayModeTests
    {
        static ScheduleConfirmButton FindConfirm(ScheduleType type)
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<ScheduleConfirmButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.Type == type) return c;
            return null;
        }

        [UnityTest]
        public IEnumerator ExerciseTab_Confirm_Yes_Publishes_ScheduleSelected()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<LoveAlgo.Game.GameBootstrap>();
            if (bootstrap != null) bootstrap.PrologueCsv = ""; // 프롤로그 스킵
            yield return null;

            // 운동 확인 버튼(Exercise_A) 활성까지 대기(부팅→스케줄 페이즈).
            ScheduleConfirmButton confirm = null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                confirm = FindConfirm(ScheduleType.Exercise_A);
                if (confirm != null && confirm.isActiveAndEnabled) break;
                confirm = null;
                yield return null;
            }
            Assert.IsNotNull(confirm, "운동 탭 확인 버튼(Exercise_A)이 활성화되어야 함");

            var modal = UnityEngine.Object.FindAnyObjectByType<ModalView>();
            Assert.IsNotNull(modal, "씬에 ModalView 존재");

            ScheduleType? selected = null;
            var sub = EventBus.Subscribe<ScheduleSelectedCommand>(e => selected = e.Type);
            try
            {
                confirm.GetComponent<Button>().onClick.Invoke(); // 확인 팝업 표시
                yield return null;

                Assert.IsTrue(modal.Root.activeSelf, "운동 탭 클릭 → 확인 모달 표시");
                Assert.AreEqual(ScheduleTable.FormatEffect(ScheduleType.Exercise_A), modal.MessageText.text,
                    "두 번째 메시지 = 효과 요약");
                Assert.IsNull(selected, "확인(예) 전엔 스케줄 미실행");

                var buttons = modal.ButtonContainer.GetComponentsInChildren<Button>();
                Assert.AreEqual(2, buttons.Length, "아니오/예 2버튼 생성");
                buttons[1].onClick.Invoke(); // 예(index 1)
                yield return null;

                Assert.AreEqual(ScheduleType.Exercise_A, selected, "예 → Exercise_A 실행 명령(ScheduleSelectedCommand) 발행");
            }
            finally
            {
                sub.Dispose();
            }
        }
    }
}
