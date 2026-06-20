using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate
using LoveAlgo.Events; // ShowSaveLoadCommand, OpenDialogueLogCommand, SetAutoModeCommand, ShowStageLayerCommand, ShowDialogueCommand, CompletionHandle
using LoveAlgo.UI;     // DialogueButtonBarView, DialogueView, StageLayerView, SaveLoadView, DialogueLogView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 대사창 버튼 바 계약: ① 버튼 7종(타이틀/불러오기/설정/저장/로그/오토/숨기기)이 명령만 발행(ADR-007)하고
    /// 오토 토글이 아이콘을 스왑 ② 숨기기 → 슬라이드 다운+보이기 버튼 노출+오토 일시정지, 복원 입력은 진행으로
    /// 소비되지 않음 ③ CG 진입 시 오토모드 정지(인벤토리 §CG).
    /// </summary>
    public class DialogueButtonBarPlayModeTests
    {
        readonly List<GameObject> _roots = new();
        readonly List<IDisposable> _subs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            foreach (var r in _roots) if (r != null) UnityEngine.Object.DestroyImmediate(r);
            _roots.Clear();
            EventBus.Publish(new SetAutoModeCommand(false)); // 상주 DialogueView 오토 상태 원복
            OverlayGate.Reset();
        }

        // 같은 명령에 반응하는 상주 씬 구독자 중화(이중 처리/게이트 오염 방지) — 기존 테스트 수칙.
        static void DestroyResidents<T>() where T : MonoBehaviour
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                UnityEngine.Object.DestroyImmediate(v.gameObject);
        }

        DialogueButtonBarView CreateBar(Sprite on, Sprite off)
        {
            var go = new GameObject("ButtonBar_Test", typeof(RectTransform));
            _roots.Add(go);
            go.SetActive(false); // Awake 전 주입

            var bar = go.AddComponent<DialogueButtonBarView>();
            Button Mk(string name)
            {
                var b = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                b.transform.SetParent(go.transform, false);
                return b.GetComponent<Button>();
            }
            bar.TitleButton = Mk("Title");
            bar.LoadButton = Mk("Load");
            bar.ConfigButton = Mk("Config");
            bar.SaveButton = Mk("Save");
            bar.LogButton = Mk("Log");
            bar.AutoButton = Mk("Auto");
            bar.HideButton = Mk("Hide");
            bar.AutoIcon = bar.AutoButton.GetComponent<Image>();
            bar.AutoOnSprite = on;
            bar.AutoOffSprite = off;

            go.SetActive(true); // Awake(버튼 바인딩)+OnEnable(구독)
            return bar;
        }

        [UnityTest]
        public IEnumerator Buttons_PublishCommands_AndAutoToggleSwapsIcon()
        {
            DestroyResidents<SaveLoadView>();    // 세이브/불러오기 명령에 팝업이 열려 게이트를 밀지 않게
            DestroyResidents<DialogueLogView>(); // 로그 명령 동일

            var on = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            var off = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            var bar = CreateBar(on, off);
            yield return null;

            var saveModes = new List<SaveLoadMode>();
            int logOpens = 0, titleReturns = 0, settingsOpens = 0;
            ShowModalCommand? titleModal = null;
            var autoLog = new List<bool>();
            _subs.Add(EventBus.Subscribe<ShowSaveLoadCommand>(e => saveModes.Add(e.Mode)));
            _subs.Add(EventBus.Subscribe<OpenDialogueLogCommand>(_ => logOpens++));
            _subs.Add(EventBus.Subscribe<SetAutoModeCommand>(e => autoLog.Add(e.On)));
            _subs.Add(EventBus.Subscribe<ReturnToTitleCommand>(_ => titleReturns++));
            _subs.Add(EventBus.Subscribe<ShowModalCommand>(e => titleModal = e));
            _subs.Add(EventBus.Subscribe<ShowSettingsCommand>(_ => settingsOpens++));

            // 타이틀 버튼 → 확인 모달(즉시 복귀 아님). "예"(index 1)일 때만 ReturnToTitle.
            bar.TitleButton.onClick.Invoke();
            Assert.AreEqual(0, titleReturns, "타이틀 버튼 클릭만으론 복귀 안 함(확인 모달 경유)");
            Assert.IsTrue(titleModal.HasValue, "타이틀 버튼 → 확인 모달 발행");
            titleModal.Value.Handle.Select(0); // 아니오 → 복귀 없음
            Assert.AreEqual(0, titleReturns, "아니오 선택 시 타이틀 복귀 없음");
            bar.TitleButton.onClick.Invoke();
            titleModal.Value.Handle.Select(1); // 예 → 복귀
            Assert.AreEqual(1, titleReturns, "예 선택 시 → ReturnToTitle");

            bar.ConfigButton.onClick.Invoke();
            Assert.AreEqual(1, settingsOpens, "설정 버튼 → 설정 열기 명령");

            bar.SaveButton.onClick.Invoke();
            bar.LoadButton.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { SaveLoadMode.Save, SaveLoadMode.Load }, saveModes, "저장/불러오기 버튼 → 각 모드 팝업 명령");

            bar.LogButton.onClick.Invoke();
            Assert.AreEqual(1, logOpens, "로그 버튼 → 로그 열기 명령");

            Assert.AreEqual(off, bar.AutoIcon.sprite, "초기 오토 OFF 아이콘");
            bar.AutoButton.onClick.Invoke();
            Assert.AreEqual(new[] { true }, autoLog, "오토 버튼 → ON 발행");
            Assert.AreEqual(on, bar.AutoIcon.sprite, "ON 아이콘 스왑(자기 발행 구독 미러)");

            bar.AutoButton.onClick.Invoke();
            Assert.AreEqual(new[] { true, false }, autoLog, "재클릭 → OFF 발행");
            Assert.AreEqual(off, bar.AutoIcon.sprite, "OFF 아이콘 복귀");
        }

        DialogueView CreateDialogueView(out GameObject visualRoot) => CreateDialogueView(out visualRoot, out _);

        DialogueView CreateDialogueView(out GameObject visualRoot, out GameObject showBtn)
        {
            var holder = new GameObject("DialogueView_HideTest", typeof(RectTransform));
            _roots.Add(holder);
            holder.SetActive(false);

            var view = holder.AddComponent<DialogueView>();
            visualRoot = new GameObject("Root", typeof(RectTransform));
            visualRoot.transform.SetParent(holder.transform, false);
            view.Root = visualRoot;
            // 보이기 버튼(Root 형제 — 슬라이드와 무관하게 제자리). 부팅 시 OnEnable이 끈다.
            showBtn = new GameObject("ShowButton", typeof(RectTransform));
            showBtn.transform.SetParent(holder.transform, false);
            view.ShowButton = showBtn;

            holder.SetActive(true);
            // 활성화 "후" 주입 — OnEnable의 ApplyFromSettings()가 영속 속도로 덮어쓰는 함정(2026-06-11 실증) 회피.
            view.CharInterval = 0f;   // 즉시 표시 — 대기 로직만 검증
            view.SlideDuration = 0f;  // 즉시 스냅 — 슬라이드 코루틴 대기 없이 결정적
            return view;
        }

        [UnityTest]
        public IEnumerator HideByUser_SlidesDown_ShowsButton_RestoreInput_NotConsumedAsAdvance()
        {
            DestroyResidents<DialogueView>(); // 같은 ShowDialogueCommand 이중 처리 방지

            var view = CreateDialogueView(out var root, out var showBtn);
            var rootRt = (RectTransform)root.transform;
            float homeY = rootRt.anchoredPosition.y;
            Assert.IsFalse(showBtn.activeSelf, "부팅 — 보이기 버튼 숨김");
            yield return null;

            EventBus.Publish(new SetAutoModeCommand(false));
            var req = new CompletionHandle();
            EventBus.Publish(new ShowDialogueCommand("로아", "안녕!", true, req));
            yield return null;
            Assert.IsTrue(root.activeSelf, "대사 표시 중");

            view.HideByUser();
            Assert.IsTrue(view.IsHiddenByUser);
            Assert.IsTrue(root.activeSelf, "숨기기 → 비주얼은 active 유지(슬라이드로 사라짐, SetActive 아님)");
            Assert.Less(rootRt.anchoredPosition.y, homeY, "패널이 아래로 슬라이드(홈보다 낮은 y)");
            Assert.IsTrue(showBtn.activeSelf, "기존 대사창 위치에 보이기 버튼 노출");
            Assert.IsFalse(req.IsComplete, "숨기기는 진행이 아님");

            view.RestoreByUser(); // 보이기 버튼 onClick 경로(public)
            Assert.IsFalse(view.IsHiddenByUser, "보이기 → 복원");
            Assert.IsFalse(showBtn.activeSelf, "복원 시 보이기 버튼 숨김");
            Assert.AreEqual(homeY, rootRt.anchoredPosition.y, 0.01f, "패널 원위치 복귀");
            Assert.IsFalse(req.IsComplete, "복원 입력은 진행으로 소비되지 않음");

            view.Advance("좌클릭");
            yield return null;
            Assert.IsTrue(req.IsComplete, "복원 후 다음 입력이 진행");
        }

        [UnityTest]
        public IEnumerator HideByUser_PausesAutoMode_UntilRestore()
        {
            DestroyResidents<DialogueView>();

            var view = CreateDialogueView(out _);
            view.AutoAdvanceDelay = 0.05f;
            yield return null;

            EventBus.Publish(new SetAutoModeCommand(true));
            var req = new CompletionHandle();
            EventBus.Publish(new ShowDialogueCommand("로아", "안녕!", true, req));
            yield return null;

            view.HideByUser();
            float t = 0f;
            while (t < 0.3f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(req.IsComplete, "숨김 중 오토 일시정지(스토리가 흘러가지 않음)");

            view.Advance("좌클릭"); // 복원 → 오토 재개
            t = 0f;
            while (!req.IsComplete && t < 1f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(req.IsComplete, "복원 후 오토 재개 → 자동 진행");
        }

        [UnityTest]
        public IEnumerator CgEnter_StopsAutoMode()
        {
            DestroyResidents<StageLayerView>(); // 같은 명령 이중 발행 방지
            DestroyResidents<DialogueView>();   // CG 모드 부수효과(루트 토글) 무관화

            var go = new GameObject("StageLayer_Test", typeof(RectTransform));
            _roots.Add(go);
            go.SetActive(false);
            var view = go.AddComponent<StageLayerView>();
            var img = new GameObject("CG", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(go.transform, false);
            view.CgImage = img;
            go.SetActive(true);
            yield return null;

            var autoLog = new List<bool>();
            _subs.Add(EventBus.Subscribe<SetAutoModeCommand>(e => autoLog.Add(e.On)));
            EventBus.Publish(new SetAutoModeCommand(true)); // 오토 켜진 상태에서

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.CG, false, "no_such_cg", LayerTransition.Cut, 0f, handle));
            yield return null;

            Assert.Contains(false, autoLog, "CG 진입 → 오토모드 정지 발행(인벤토리 §CG)");
            EventBus.Publish(new SetCgModeCommand(false)); // 상주 구독자 상태 원복(안전)
        }
    }
}
