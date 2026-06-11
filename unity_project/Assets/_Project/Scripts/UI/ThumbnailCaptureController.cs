using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // JsonSaveStore
using LoveAlgo.Events; // CaptureThumbnailCommand
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
            StartCoroutine(ThumbnailCapture.CaptureToFile(path));
        }
    }
}
