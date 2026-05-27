using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 대사 색 프리셋 — 디자이너 친화 named color (Phase D13).
    /// CSV 대사에 `&lt;color=heroine_roa&gt;...&lt;/color&gt;` 같이 쓰면 SO에 정의된 색으로 치환.
    /// 미정의 이름은 LogWarning + 원본 그대로 통과 (게임 안 멈춤).
    ///
    /// 사용:
    ///   1) Project 우클릭 → Create → LoveAlgo/Dialogue Color Palette
    ///   2) Resources/Data/DialogueColorPalette.asset 으로 저장 (이름 고정)
    ///   3) entries에 (name, color) 추가
    ///   4) 대사 CSV에 &lt;color=name&gt; 사용
    ///
    /// 폴백:
    ///   - SO 없거나 비어 있으면 hex 색만 동작 (역호환 — 기존 &lt;color=#xxxxxx&gt; 그대로)
    ///   - 모든 미정의 이름도 그대로 TMP로 — TMP가 모르는 색이름이면 검정 처리하지만
    ///     LogWarning이 사전 경고 역할.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueColorPalette", menuName = "LoveAlgo/Dialogue Color Palette")]
    public class DialogueColorPaletteSO : ScriptableObject
    {
        [SerializeField] List<Entry> entries = new();

        [Serializable]
        public class Entry
        {
            [Tooltip("CSV에서 사용할 이름. 대소문자 무시 비교. 예: 'heroine_roa', 'system', 'warning'")]
            public string name;

            [Tooltip("대응 색. 알파 포함.")]
            public Color color = Color.white;

            [Tooltip("기획자 메모 — 코드 무시.")]
            public string notes;
        }

        /// <summary>이름→색 dictionary (대소문자 무시). 매번 새로 만들지 말고 캐시하려면 호출자가 결과 저장.</summary>
        public Dictionary<string, Color> BuildLookup()
        {
            var dict = new Dictionary<string, Color>(entries.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrWhiteSpace(e.name)) continue;
                dict[e.name.Trim()] = e.color;
            }
            return dict;
        }
    }
}
