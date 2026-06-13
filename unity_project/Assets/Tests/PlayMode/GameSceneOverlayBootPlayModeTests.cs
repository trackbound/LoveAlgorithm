using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Core;     // OverlayGate
using LoveAlgo.Events;   // ShowSaveLoadCommand, ShowModalCommand, ModalButton, ModalRequest
using LoveAlgo.Settings; // SettingsView
using LoveAlgo.Shop;     // ShopView
using LoveAlgo.UI;       // SaveLoadView, QuickMenuView, ModalView, LoadingScreenView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// "에디터=inactive 저장 + 부팅 일괄 활성화" 정책의 씬 회귀 가드(GameSceneSimulation 패턴 미러).
    /// ① 부팅 후 Overlay 축 뷰 6종이 활성(=UiBootActivator 작동, 구독 가능) ② 페이즈 그룹은 불침범
    /// (Story/Ending inactive·Simulation active — UIManager 영역) ③ 열기 명령 왕복(표시→게이트→뒤로가기 닫기).
    /// 씬흐름/종료 명령은 발행하지 않는다(잔존 SceneFlowController 함정 — 모달 "예"(Quit) 경로 금지).
    /// </summary>
    public class GameSceneOverlayBootPlayModeTests
    {
        [TearDown]
        public void TearDown() => OverlayGate.Reset();

        [UnityTest]
        public IEnumerator GameScene_OverlayViews_AliveAfterBoot_AndOpenCloseRoundtrip()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<LoveAlgo.Game.GameBootstrap>();
            if (bootstrap != null) bootstrap.PrologueCsv = ""; // 프롤로그 스킵 — 부팅 활성화만 격리 검증
            yield return null; // Start 부팅(UiBootActivator는 Awake에서 이미 완료)

            // ① Overlay 축 6종 활성(FindAnyObjectByType 기본 = 활성만 탐색 → 발견 = 부팅 활성 증명)
            Assert.IsNotNull(Object.FindAnyObjectByType<SaveLoadView>(), "SaveLoadView 부팅 활성(구독 가능)");
            Assert.IsNotNull(Object.FindAnyObjectByType<SettingsView>(), "SettingsView 부팅 활성");
            Assert.IsNotNull(Object.FindAnyObjectByType<ShopView>(), "ShopView 부팅 활성");
            Assert.IsNotNull(Object.FindAnyObjectByType<QuickMenuView>(), "QuickMenuView 부팅 활성");
            var bootModal = Object.FindAnyObjectByType<ModalView>();
            Assert.IsNotNull(bootModal, "ModalView 부팅 활성");
            Assert.IsNotNull(Object.FindAnyObjectByType<LoadingScreenView>(), "LoadingScreenView 부팅 활성");
            Assert.IsNotNull(Object.FindAnyObjectByType<DialogueLogView>(), "DialogueLogView 부팅 활성(휠업 로그)");
            Assert.IsNotNull(Object.FindAnyObjectByType<UsernameScreenView>(), "UsernameScreenView 부팅 활성(이름 입력)");

            // 빠른메뉴 노출 규칙: 시뮬 직행 부팅 = Schedule 페이즈(GameStateData 기본) → 표시.
            // (프롤로그 부팅이면 같은 프레임 Story 전환으로 숨김 — 여기선 PrologueCsv="" 경로.)
            // 직전 테스트가 남긴 stale 페이즈를 OnEnable이 읽을 수 있어 부팅 재평가는 Start+1프레임 —
            // 그 프레임을 기다린 뒤 판정한다.
            var quickMenu = Object.FindAnyObjectByType<QuickMenuView>();
            yield return null;
            Assert.IsTrue(quickMenu.IsShown, "스케줄 화면 → 빠른메뉴 표시(서브메뉴 기획서)");

            // 활성화 ≠ 노출: 비주얼은 전부 부팅 숨김이어야 한다(authored-active 저장 방어 — placeholder 노출 사고 가드).
            Assert.IsFalse(bootModal.Root != null && bootModal.Root.activeSelf, "모달 비주얼 부팅 숨김");
            Assert.IsFalse(OverlayGate.IsBlocked, "부팅 직후 게이트 비차단(팝업 전부 자체 숨김)");

            // 홀더-비주얼 분리 무결성: root류가 뷰 GO 자신이면 숨김이 구독까지 죽인다
            // (Modal·Choice 2회 실증 사고 — 씬 재배선 시 여기서 먼저 깨지게 못박는다).
            foreach (var m in Object.FindObjectsByType<ModalView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.AreNotEqual(m.gameObject, m.Root, $"{m.name}: ModalView.Root는 비주얼 자식이어야 함");
            foreach (var c in Object.FindObjectsByType<ChoiceView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assert.AreNotEqual(c.gameObject, c.Root, $"{c.name}: ChoiceView.Root는 비주얼 자식이어야 함");
                Assert.AreNotEqual(c.gameObject, c.Dim, $"{c.name}: ChoiceView.Dim은 비주얼 자식이어야 함");
            }
            foreach (var mv in Object.FindObjectsByType<LoveAlgo.Messenger.MessengerView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.AreNotEqual(mv.gameObject, mv.Root, $"{mv.name}: MessengerView.Root는 비주얼 자식이어야 함");

            // ② 페이즈 그룹 불침범(UIManager 영역) — Story/Ending inactive·Simulation active 유지
            var ui = GameObject.Find("_UI");
            Assert.IsNotNull(ui, "_UI 루트 존재");
            Assert.IsFalse(ui.transform.Find("Story").gameObject.activeSelf, "Story는 부팅 inactive 유지");
            Assert.IsFalse(ui.transform.Find("Ending").gameObject.activeSelf, "Ending은 부팅 inactive 유지");
            Assert.IsTrue(ui.transform.Find("Simulation").gameObject.activeSelf, "Simulation은 부팅 active 유지");

            // ③ 열기 명령 왕복: 세이브로드 표시 → 게이트 차단 → 공용 뒤로가기로 닫기
            EventBus.Publish(new ShowSaveLoadCommand(SaveLoadMode.Load));
            var saveLoad = Object.FindAnyObjectByType<SaveLoadView>();
            var group = saveLoad.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "SaveLoadPopup 루트 CanvasGroup");
            Assert.AreEqual(1f, group.alpha, 1e-3f, "열기 명령 → 표시(alpha 1)");
            Assert.IsTrue(OverlayGate.IsBlocked, "표시 중 게임플레이 입력 차단");

            Assert.IsTrue(OverlayGate.CloseTop(), "공용 뒤로가기 → 최상단 닫기");
            Assert.AreEqual(0f, group.alpha, 1e-3f, "닫힘(alpha 0)");
            Assert.IsFalse(OverlayGate.IsBlocked, "게이트 해제");
        }

        [UnityTest]
        public IEnumerator TitleScene_Modal_AliveAfterBoot_ShowsOnCommand()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;

            var modal = Object.FindAnyObjectByType<ModalView>();
            Assert.IsNotNull(modal, "Title ModalView 부팅 활성(구독 가능) — Exit 확인 모달의 전제");
            Assert.IsFalse(modal.Root != null && modal.Root.activeSelf,
                "타이틀 부팅 시 모달 비주얼 숨김(authored-active 저장 방어 — placeholder 노출 사고 가드)");

            // 표시 왕복 — Quit 경로(예 선택) 금지: 닫기 버튼 1개 모달로 검증.
            var handle = new ModalRequest();
            EventBus.Publish(new ShowModalCommand("테스트", "부팅 활성 검증",
                new[] { new ModalButton("닫기", ModalButtonKind.Close) }, handle));
            Assert.IsNotNull(modal.Root, "모달 비주얼 루트 바인딩");
            Assert.IsTrue(modal.Root.activeSelf, "표시 명령 → 모달 표시");

            // 정리: GO 토글로 OnDisable 경로(Clear+root off) 실행 후 재구독 복원 — 상주 상태 원복.
            modal.gameObject.SetActive(false);
            modal.gameObject.SetActive(true);
            Assert.IsFalse(modal.Root.activeSelf, "정리 후 모달 숨김(상주 상태 원복)");
        }
    }
}
