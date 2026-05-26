using System.Collections.Generic;
using LoveAlgo.Common;
using LoveAlgo.Contracts;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Modules.Affinity
{
    /// <summary>
    /// AffinityChangedEvent를 받아 토스트로 보여주는 알리미.
    /// 일반 변화 → 짧은 토스트 ("로아 +2 호감도"),
    /// 등급 상승 → 길고 강조된 토스트 ("로아 — 친구가 됐어요").
    ///
    /// AfterSceneLoad에서 자동 부트스트랩 — 씬에 별도 GameObject 배치 불필요.
    /// PopupManager가 아직 준비되지 않았을 때(타이틀 직전 등)는 토스트 호출이
    /// no-op으로 흘러가므로 안전.
    /// </summary>
    public class AffinityToastNotifier : MonoBehaviour
    {
        /// <summary>등급 상승 토스트 표시 시간 (일반보다 살짝 길게).</summary>
        const float TierUpToastDuration = 3.2f;

        /// <summary>일반 변화 토스트 표시 시간.</summary>
        const float DefaultToastDuration = 1.8f;

        /// <summary>마지막으로 본 히로인별 등급 — 등급 상승 감지용.</summary>
        readonly Dictionary<string, AffinityTier> _lastTier = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // 헤드리스/테스트 환경에선 토스트 자체가 의미 없으니 건너뜀.
            if (Headless.IsEnabled) return;

            var go = new GameObject("[AffinityToastNotifier]");
            DontDestroyOnLoad(go);
            go.AddComponent<AffinityToastNotifier>();
        }

        void Awake()
        {
            // 시작 시점의 등급을 캐싱 — 첫 이벤트에서 false-positive "등급 상승" 방지.
            CaptureInitialTiers();
            this.SubscribeOnDestroy<AffinityChangedEvent>(OnAffinityChanged);
        }

        void CaptureInitialTiers()
        {
            var aff = Services.TryGet<IAffinity>();
            if (aff == null) return;
            foreach (var info in aff.GetAll())
            {
                _lastTier[info.HeroineId] = info.Tier;
            }
        }

        void OnAffinityChanged(AffinityChangedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.HeroineId)) return;
            if (PopupManager.Instance == null) return;

            string displayName = ResolveDisplayName(evt.HeroineId);
            AffinityTier currentTier = ResolveCurrentTier(evt.HeroineId);
            bool tierUp = _lastTier.TryGetValue(evt.HeroineId, out var prev) && currentTier > prev;
            _lastTier[evt.HeroineId] = currentTier;

            if (tierUp)
            {
                PopupManager.Instance.Toast(
                    $"💕 {displayName}",
                    TierUpMessage(currentTier),
                    TierUpToastDuration);
                return;
            }

            // 0점 변화는 보통 SyncToGameState만 일어난 경우 — 토스트 생략.
            if (evt.Delta == 0) return;

            string sign = evt.Delta > 0 ? "+" : "";
            PopupManager.Instance.Toast(
                displayName,
                $"호감도 {sign}{evt.Delta}",
                DefaultToastDuration);
        }

        string ResolveDisplayName(string heroineId)
        {
            if (GameConstants.HeroineById.TryGetValue(heroineId, out var cfg))
                return cfg.DisplayName;
            return heroineId;
        }

        AffinityTier ResolveCurrentTier(string heroineId)
        {
            // IAffinity가 있으면 거기 거치고, 없을 땐 정적 폴백 (테스트/부트 직후 대비).
            var aff = Services.TryGet<IAffinity>();
            if (aff != null) return aff.Get(heroineId).Tier;
            return AffinityCalculator.GetTier(heroineId);
        }

        public static string TierUpMessage(AffinityTier tier) => tier switch
        {
            AffinityTier.Acquaintance => "아는 사이가 됐어요",
            AffinityTier.Friend       => "친구가 됐어요",
            AffinityTier.CloseFriend  => "가까운 친구가 됐어요",
            AffinityTier.Love         => "마음이 통했어요",
            _                         => "한 발짝 가까워졌어요",
        };
    }
}
