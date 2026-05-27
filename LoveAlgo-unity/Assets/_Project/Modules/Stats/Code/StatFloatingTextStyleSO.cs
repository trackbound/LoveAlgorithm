using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Modules.Stats
{
    /// <summary>
    /// 스탯 변화 floating text 스타일 정의 (Phase D6).
    /// 색 = 데이터 드리븐. 기획자가 스탯별 색·접두사를 SO에서 조정.
    ///
    /// 사용:
    ///   1) Project 우클릭 → Create → LoveAlgo/Stat Floating Text Style
    ///   2) Resources/Data/StatFloatingTextStyle.asset 으로 저장 (이름 고정)
    ///   3) overrides에 스탯별 (statId, gainColor, lossColor) 추가
    ///   4) 미등록 스탯은 default {gain,loss}Color 사용
    ///
    /// SO 없으면 StatFloatingTextNotifier가 코드 기본값(녹/빨/파)으로 폴백 — 즉시 가용.
    /// </summary>
    [CreateAssetMenu(fileName = "StatFloatingTextStyle", menuName = "LoveAlgo/Stat Floating Text Style")]
    public class StatFloatingTextStyleSO : ScriptableObject
    {
        [Header("기본 색 (statId 매칭 없을 때)")]
        [Tooltip("Delta>0 일 때 기본 색.")]
        public Color defaultGainColor = new Color(0.45f, 0.95f, 0.45f);

        [Tooltip("Delta<0 일 때 기본 색.")]
        public Color defaultLossColor = new Color(0.95f, 0.45f, 0.45f);

        [Header("표시 옵션")]
        [Tooltip("폰트 크기. 0이하면 60 폴백.")]
        public float fontSize = 56f;

        [Tooltip("Y축 떠오를 거리(px). 위로 음수면 아래로.")]
        public float floatDistance = 120f;

        [Tooltip("애니메이션 총 시간(초). 페이드아웃 포함.")]
        public float lifetime = 1.0f;

        [Tooltip("앵커 위치 정규화 (0~1, screen-space). (0.5, 0.5)=중앙. (0.95, 0.5)=우측중앙.")]
        public Vector2 anchor = new Vector2(0.85f, 0.55f);

        [Header("스탯별 override (선호 — 미등록 시 기본 색 사용)")]
        [Tooltip("특정 statId (예: Fatigue)에 대해 색을 뒤집고 싶을 때.")]
        public List<Override> overrides = new();

        [Serializable]
        public class Override
        {
            [Tooltip("매칭할 statId (예: Str, Int, Fatigue). 대소문자 무시.")]
            public string statId;

            [Tooltip("Delta>0 일 때 색. 알파 0이면 default 사용.")]
            public Color gainColor = Color.green;

            [Tooltip("Delta<0 일 때 색. 알파 0이면 default 사용.")]
            public Color lossColor = Color.red;
        }

        public Color ResolveColor(string statId, int delta)
        {
            if (delta == 0) return defaultGainColor; // 거의 안 일어남 — 안전 폴백

            // 매칭되는 override 찾기 (case-insensitive)
            for (int i = 0; i < overrides.Count; i++)
            {
                var o = overrides[i];
                if (o == null) continue;
                if (string.Equals(o.statId, statId, StringComparison.OrdinalIgnoreCase))
                {
                    var c = delta > 0 ? o.gainColor : o.lossColor;
                    // 알파 0 → "사용 안 함" 신호 → default로 폴백
                    if (c.a > 0.01f) return c;
                    break;
                }
            }
            return delta > 0 ? defaultGainColor : defaultLossColor;
        }
    }
}
