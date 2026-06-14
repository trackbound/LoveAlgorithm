using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // JsonSaveStore
using LoveAlgo.Events; // CaptureThumbnailCommand, ThumbnailSavedEvent
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 썸네일 캡처 어댑터(MonoBehaviour, 게임 씬). <see cref="CaptureThumbnailCommand"/>를 구독해
    /// <see cref="ThumbnailCapture"/> 코루틴으로 슬롯 PNG를 비동기 기록한다(ADR-007 얇은 어댑터).
    /// 게임 씬 _UI/_Boot에 배선(Game.unity 안정화 후) — Title에는 불필요(저장은 인게임만).
    /// </summary>
    public class ThumbnailCaptureController : MonoBehaviour
    {
        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<CaptureThumbnailCommand>(OnCapture);
        void OnDisable() { _sub?.Dispose(); _sub = null; }

        void OnCapture(CaptureThumbnailCommand e)
        {
            string path = JsonSaveStore.ThumbnailPath(JsonSaveStore.ThumbnailFileFor(e.Slot));
            if (!isActiveAndEnabled) return; // 코루틴 시작 불가 시 스킵(다음 저장에 재시도)
            StartCoroutine(CaptureThenNotify(e.Slot, path));
        }

        // 캡처(프레임 종료 대기)가 끝난 뒤 통지 — 열려 있는 세이브 팝업이 저장 직후 썸네일을 바로 반영하도록.
        IEnumerator CaptureThenNotify(int slot, string path)
        {
            yield return ThumbnailCapture.CaptureToFile(path);
            EventBus.Publish(new ThumbnailSavedEvent(slot));
        }
    }
}
