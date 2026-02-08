using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로그 팝업 — 그룹 항목 UI
    ///
    /// 같은 화자의 연속 대사를 하나의 LogEntryUI로 묶는다.
    /// 헤더 프리팹(고정 크기) 1개 + 우측 DialogueColumn에 버블 N개.
    ///
    /// LogEntry (HorizontalLayoutGroup)
    /// ├── [Header prefab instance]            ← 케이스별 고정크기 프리팹
    /// ├── DialogueColumn (VLG, spacing=8)     ← 버블 부모
    /// └── BubbleTemplate (비활성, 클론 원본)
    ///     ├── VLG + Image (텍스트박스 BG) + LE
    ///     └── MessageText (TMP) + Shadow
    /// </summary>
    public class LogEntryUI : MonoBehaviour
    {
        [Header("헤더 프리팹 (고정 크기)")]
        [SerializeField] GameObject headerCharacterPrefab;  // 프로필+네임박스
        [SerializeField] GameObject headerExtraPrefab;      // 네임박스만
        [SerializeField] GameObject headerUserPrefab;       // 주인공 네임박스

        [Header("대사 버블")]
        [SerializeField] RectTransform dialogueColumn;
        [SerializeField] GameObject bubbleTemplate;

        // 에셋 (LogPopup에서 주입)
        Sprite characterTextboxSprite;
        Sprite userTextboxSprite;
        TMP_FontAsset narrationFont;
        TMP_FontAsset defaultFont;

        // 현재 모드
        Sprite activeTextboxSprite;
        Color activeTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        bool isNarration;

        /// <summary>에셋 주입 (Instantiate 직후 1회)</summary>
        public void SetAssets(
            Sprite charTextbox, Sprite userTextbox,
            TMP_FontAsset narrationFontAsset)
        {
            characterTextboxSprite = charTextbox;
            userTextboxSprite = userTextbox;
            narrationFont = narrationFontAsset;
        }

        // ── 헤더 설정 (그룹당 1회) ───────────────────────

        /// <summary>케이스 1: 캐릭터 + 초상화</summary>
        public void SetCharacterWithPortrait(string speaker, Sprite portrait)
        {
            var header = SpawnHeader(headerCharacterPrefab);
            if (header != null)
            {
                header.SetPortrait(portrait);
                header.SetName(speaker);
            }

            activeTextboxSprite = characterTextboxSprite;
            activeTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            isNarration = false;
        }

        /// <summary>케이스 2: 엑스트라 (초상화 없음)</summary>
        public void SetExtraEntry(string speaker)
        {
            var header = SpawnHeader(headerExtraPrefab);
            if (header != null)
                header.SetName(speaker);

            activeTextboxSprite = characterTextboxSprite;
            activeTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            isNarration = false;
        }

        /// <summary>케이스 3: 주인공</summary>
        public void SetUserEntry(string speaker)
        {
            var header = SpawnHeader(headerUserPrefab);
            if (header != null)
                header.SetName(speaker);

            activeTextboxSprite = userTextboxSprite;
            activeTextColor = Color.white;
            isNarration = false;
        }

        /// <summary>케이스 4: 독백 (헤더 없음, 배경 없음, 그림자+Aggro Light)</summary>
        public void SetNarrationMode()
        {
            // 헤더 프리팹 없음 — DialogueColumn만 사용
            activeTextColor = Color.white;
            isNarration = true;
        }

        // ── 대사 추가 (그룹 내 N회 호출 가능) ────────────

        /// <summary>대사 버블 1개 추가</summary>
        public void AddLine(string text)
        {
            if (bubbleTemplate == null || dialogueColumn == null) return;

            var go = Instantiate(bubbleTemplate, dialogueColumn);
            go.SetActive(true);

            var bg = go.GetComponent<Image>();
            var tmp = go.GetComponentInChildren<TMP_Text>();
            if (tmp == null) return;

            // 기본 폰트 캐시 (최초 1회)
            if (defaultFont == null) defaultFont = tmp.font;

            tmp.text = text;
            tmp.color = activeTextColor;

            var shadow = tmp.GetComponent<Shadow>();

            if (isNarration)
            {
                if (bg != null) bg.enabled = false;
                if (narrationFont != null) tmp.font = narrationFont;
                if (shadow != null) shadow.enabled = true;
            }
            else
            {
                if (bg != null && activeTextboxSprite != null)
                    bg.sprite = activeTextboxSprite;
                if (defaultFont != null) tmp.font = defaultFont;
                if (shadow != null) shadow.enabled = false;
            }
        }

        // ── 내부 헬퍼 ─────────────────────────────────────

        LogHeaderUI SpawnHeader(GameObject prefab)
        {
            if (prefab == null) return null;

            var go = Instantiate(prefab, transform);
            go.SetActive(true);
            go.transform.SetAsFirstSibling();
            return go.GetComponent<LogHeaderUI>();
        }
    }
}
