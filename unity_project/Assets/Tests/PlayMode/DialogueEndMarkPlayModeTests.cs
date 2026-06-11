using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDialogueCommand, CompletionHandle
using LoveAlgo.UI;     // DialogueView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// DialogueView 진행 아이콘(End Mark) 표시 계약: 타이핑이 끝나고 클릭 대기에 들어가면 본문 끝에 아이콘이
    /// 켜지고, 진행(Advance)하면 꺼진다. RequireClick=false(자동 연결 라인)면 표시되지 않는다.
    /// (정확한 픽셀 위치는 TMP 메시 의존이라 감독 Play 시각 검증 영역 — 여기선 켜짐/꺼짐만 검증.)
    /// </summary>
    public class DialogueEndMarkPlayModeTests
    {
        // 본문 글자가 가시여야 "마지막 글자 뒤" 배치가 성립 → TMP 기본 폰트 필요. 없으면 검증 불가로 Ignore.
        static bool HasFont => TMP_Settings.defaultFontAsset != null;

        GameObject Build(out DialogueView dlg, out RectTransform mark)
        {
            foreach (var v in Object.FindObjectsByType<DialogueView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);

            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            var rootGo = new GameObject("DialogueView", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            rootGo.SetActive(false); // 바인딩 후 활성화 → OnEnable 타이밍 정렬

            dlg = rootGo.AddComponent<DialogueView>();
            dlg.CharInterval = 0f; // 즉시 표시(타이핑 루프 생략)
            dlg.Root = rootGo;

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(rootGo.transform, false);
            var body = bodyGo.AddComponent<TextMeshProUGUI>();
            if (HasFont) body.font = TMP_Settings.defaultFontAsset;
            ((RectTransform)bodyGo.transform).sizeDelta = new Vector2(600, 100);
            dlg.BodyText = body;

            var markGo = new GameObject("NextIndicator", typeof(RectTransform));
            markGo.transform.SetParent(rootGo.transform, false);
            mark = (RectTransform)markGo.transform;
            mark.sizeDelta = new Vector2(33, 29);
            dlg.EndMark = mark;

            rootGo.SetActive(true);
            return canvasGo;
        }

        [UnityTest]
        public IEnumerator EndMark_Shows_AfterTyping_AndHides_OnAdvance()
        {
            if (!HasFont) Assert.Ignore("TMP 기본 폰트 미설치 — 글자 가시성 검증 불가");
            var canvas = Build(out var dlg, out var mark);
            yield return null; // OnEnable(구독 + 초기 숨김)

            Assert.IsFalse(mark.gameObject.activeSelf, "대사 시작 전 — 아이콘 숨김");

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowDialogueCommand("로아", "안녕 테스트야", true, handle));
            yield return null;

            Assert.IsTrue(mark.gameObject.activeSelf, "타이핑 완료 + 클릭 대기 → 진행 아이콘 표시");
            Assert.IsFalse(handle.IsComplete, "클릭 전엔 미완료");

            dlg.Advance("test");
            yield return null;
            yield return null;

            Assert.IsFalse(mark.gameObject.activeSelf, "진행 → 아이콘 숨김");
            Assert.IsTrue(handle.IsComplete, "진행 → 완료 핸들 풀림");

            Object.DestroyImmediate(canvas);
        }

        [UnityTest]
        public IEnumerator EndMark_NotShown_WhenNoClickRequired()
        {
            if (!HasFont) Assert.Ignore("TMP 기본 폰트 미설치 — 글자 가시성 검증 불가");
            var canvas = Build(out var dlg, out var mark);
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowDialogueCommand("로아", "자동 진행 라인", false, handle));
            yield return null;

            Assert.IsFalse(mark.gameObject.activeSelf, "RequireClick=false → 진행 아이콘 미표시");
            Assert.IsTrue(handle.IsComplete, "타이핑 완료 즉시 핸들 완료");

            Object.DestroyImmediate(canvas);
        }
    }
}
