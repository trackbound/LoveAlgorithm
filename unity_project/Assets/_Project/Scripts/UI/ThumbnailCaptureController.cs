using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // JsonSaveStore
using LoveAlgo.Events; // CaptureThumbnailCommand, PrimeThumbnailCacheCommand, ThumbnailSavedEvent
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 썸네일 캡처 어댑터(MonoBehaviour, 게임 씬). 슬롯 PNG 기록(ADR-007 얇은 어댑터). 게임 씬에 배선 —
    /// Title엔 불필요(저장은 인게임만, 캐시 예열 핸들은 그래서 미배선 시 호출부 가드로 폴백).
    ///
    /// 깜빡임 제거: 캡처는 UI 캔버스를 1프레임 끄고 백버퍼를 읽으므로(스테이지-only), 슬롯 클릭마다 하면
    /// 화면이 깜빡인다. 대신 세이브 팝업이 열리기 직전 <see cref="PrimeThumbnailCacheCommand"/>로 1회만 캡처해
    /// <see cref="_cache"/>에 담고, 수동 저장(<see cref="CaptureThumbnailCommand.UseCache"/>=true)은 캐시를
    /// 파일로 복사만 한다(토글 없음=무깜빡임). 자동저장(UseCache=false)은 현재 화면을 라이브 캡처한다.
    /// </summary>
    public class ThumbnailCaptureController : MonoBehaviour
    {
        IDisposable _subCapture;
        IDisposable _subPrime;
        byte[] _cache; // 가장 최근 캡처한 스테이지-only PNG(팝업 열기 직전 예열 또는 라이브 캡처로 갱신)

        void OnEnable()
        {
            _subCapture = EventBus.Subscribe<CaptureThumbnailCommand>(OnCapture);
            _subPrime = EventBus.Subscribe<PrimeThumbnailCacheCommand>(OnPrime);
        }

        void OnDisable()
        {
            _subCapture?.Dispose(); _subCapture = null;
            _subPrime?.Dispose(); _subPrime = null;
        }

        // 세이브 팝업 열기 직전 1회: 현재 스테이지를 캡처해 캐시. 핸들 완료로 팝업이 표시를 재개한다.
        void OnPrime(PrimeThumbnailCacheCommand e)
        {
            if (!isActiveAndEnabled) { e.Handle?.Complete(); return; } // 코루틴 불가 시 hang 방지
            StartCoroutine(PrimeRoutine(e.Handle));
        }

        IEnumerator PrimeRoutine(CompletionHandle handle)
        {
            yield return ThumbnailCapture.CaptureToBytes(bytes => { if (bytes != null) _cache = bytes; });
            handle?.Complete();
        }

        void OnCapture(CaptureThumbnailCommand e)
        {
            string path = JsonSaveStore.ThumbnailPath(JsonSaveStore.ThumbnailFileFor(e.Slot));

            // 수동 저장 + 캐시 보유 → 캡처 없이 캐시만 기록(무깜빡임).
            if (e.UseCache && _cache != null)
            {
                ThumbnailCapture.WriteBytes(path, _cache);
                EventBus.Publish(new ThumbnailSavedEvent(e.Slot));
                return;
            }

            if (!isActiveAndEnabled) return; // 코루틴 시작 불가 시 스킵(다음 저장에 재시도)
            StartCoroutine(CaptureThenNotify(e.Slot, path));
        }

        // 라이브 캡처(프레임 종료 대기) → 파일 기록 + 캐시 갱신 → 통지(열린 팝업이 썸네일 즉시 반영).
        IEnumerator CaptureThenNotify(int slot, string path)
        {
            yield return ThumbnailCapture.CaptureToBytes(bytes =>
            {
                if (bytes == null) return;
                _cache = bytes;
                ThumbnailCapture.WriteBytes(path, bytes);
            });
            EventBus.Publish(new ThumbnailSavedEvent(slot));
        }
    }
}
