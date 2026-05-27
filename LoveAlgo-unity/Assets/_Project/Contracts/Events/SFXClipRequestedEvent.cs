using UnityEngine;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// AudioClip 직접 재생 요청 (Phase C3-5).
    /// MiniGame 등 모듈이 자체 AudioClip 자산을 들고 있을 때 사용.
    /// 이름 기반 SFX는 IAudio.PlaySFX(string)을 사용 — 그쪽이 더 일반적이고 권장.
    /// Audio 모듈이 구독해 AudioManager.PlaySFXClip 호출.
    /// </summary>
    public readonly struct SFXClipRequestedEvent
    {
        public readonly AudioClip Clip;

        public SFXClipRequestedEvent(AudioClip clip)
        {
            Clip = clip;
        }
    }
}
