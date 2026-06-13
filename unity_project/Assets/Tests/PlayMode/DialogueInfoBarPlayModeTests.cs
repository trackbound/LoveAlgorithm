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
using LoveAlgo.UI;     // DialogueInfoBarView, DialogueView, StageLayerView, SaveLoadView, DialogueLogView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 대사창 인포 바 계약: ① 버튼 4종이 명령만 발행(ADR-007)하고 오토 토글이 아이콘을 스왑
    /// ② 숨기기 → 비주얼 꺼짐+오토 일시정지, 복원 입력은 진행으로 소비되지 않음
    /// ③ CG 진입 시 오토모드 정지(인벤토리 §CG).
    /// </summary>
    public class DialogueInfoBarPlayModeTests
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

        DialogueInfoBarView CreateBar(Sprite on, Sprite off)
        {
            var go = new GameObject("InfoBar_Test", typeof(RectTransform));
            _roots.Add(go);
            go.SetActive(false); // Awake 전 주입

            var bar = go.AddComponent<DialogueInfoBarView>();
            Button Mk(string name)
            {
                var b = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                b.transform.SetParent(go.transform, false);
                return b.GetComponent<Button>();
            }
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
            DestroyResidents<SaveLoadView>();    // 세이브 명령에 팝업이 열려 게이트를 밀지 않게
            DestroyResidents<DialogueLogView>(); // 로그 명령 동일

            var on = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            var off = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            var bar = CreateBar(on, off);
            yield return null;

            SaveLoadMode? saveMode = null;
            int logOpens = 0;
            var autoLog = new List<bool>();
            _subs.Add(EventBus.Subscribe<ShowSaveLoadCommand>(e => saveMode = e.Mode));
            _subs.Add(EventBus.Subscribe<OpenDialogueLogCommand>(_ => logOpens++));
            _subs.Add(EventBus.Subscribe<SetAutoModeCommand>(e => autoLog.Add(e.On)));

            bar.SaveButton.onClick.Invoke();
            Assert.AreEqual(SaveLoadMode.Save, saveMode, "세이브 버튼 → 저장 모드 팝업 명령");

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

        DialogueView CreateDialogueView(out GameObject visualRoot)
        {
            var holder = new GameObject("DialogueView_HideTest");
            _roots.Add(holder);
            holder.SetActive(false);

            var view = holder.AddComponent<DialogueView>();
            visualRoot = new GameObject("Root", typeof(RectTransform));
            visualRoot.transform.SetParent(holder.transform, false);
            view.Root = visualRoot;

            holder.SetActive(true);
            // 활성화 "후" 주입 — OnEnable의 ApplyFromSettings()가 영속 속도로 덮어쓰는 함정(2026-06-11 실증) 회피.
            view.CharInterval = 0f; // 즉시 표시 — 대기 로직만 검증
            return view;
        }

        [UnityTest]
        public IEnumerator HideByUser_RestoreInput_NotConsumedAsAdvance()
        {
            DestroyResidents<DialogueView>(); // 같은 ShowDialogueCommand 이중 처리 방지

            var view = CreateDialogueView(out var root);
            yield return null;

            EventBus.Publish(new SetAutoModeCommand(false));
            var req = new CompletionHandle();
            EventBus.Publish(new ShowDialogueCommand("로아", "안녕!", true, req));
            yield return null;
            Assert.IsTrue(root.activeSelf, "대사 표시 중");

            view.HideByUser();
            Assert.IsTrue(view.IsHiddenByUser);
            Assert.IsFalse(root.activeSelf, "숨기기 → 비주얼 꺼짐(홀더는 유지)");
            Assert.IsFalse(req.IsComplete, "숨기기는 진행이 아님");

            view.Advance("좌클릭");
            Assert.IsFalse(view.IsHiddenByUser, "입력 → 복원");
            Assert.IsTrue(root.activeSelf, "비주얼 복귀");
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
