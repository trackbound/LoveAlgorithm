#if UNITY_EDITOR
using TMPro;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 로그 UI 디자인 상수 (목업 대조용 라이브 튜닝 가능).
    /// LogUIRebuilder + LogUIDesignerWindow에서 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "LogUIDesignConfig", menuName = "LoveAlgo/Log UI Design Config")]
    public class LogUIDesignConfig : ScriptableObject
    {
        [Header("엔트리 행")]
        public int entryPadLeft = 50;
        public int entryPadRight = 30;
        public int entryPadTop = 30;
        public int entryPadBottom = 30;
        public float entrySpacing = 36f;

        [Header("헤더 (좌측 컬럼)")]
        public float headerWidth = 230f;
        public Vector2 portraitSize = new(230f, 229f);
        public Vector2 characterNameBoxSize = new(208f, 99f);
        public Vector2 userNameBoxSize = new(218f, 100f);
        public float headerPortraitToNameGap = 12f;

        [Header("버블 컬럼 — 버블 스택 여백/간격")]
        public int dialogueColumnPadLeft = 0;
        public int dialogueColumnPadRight = 0;
        public int dialogueColumnPadTop = 0;
        public int dialogueColumnPadBottom = 0;
        public float dialogueColumnSpacing = 10f;

        [Header("버블 패딩")]
        public int bubblePadLeft = 28;
        public int bubblePadRight = 28;
        public int bubblePadTop = 14;
        public int bubblePadBottom = 14;
        public float bubbleMinHeight = 60f;

        [Header("폰트 크기")]
        public float bodyFontSize = 24f;
        public float nameFontSize = 22f;
        public float narrationFontSize = 24f;

        [Header("자간 / 행간")]
        public float bodyCharacterSpacing = 0f;
        public float bodyLineSpacing = 0f;
        public float narrationLineSpacing = 0f;

        [Header("색상")]
        public Color charTextColor = new(0.18f, 0.18f, 0.22f, 1f);
        public Color userTextColor = Color.white;
        public Color narrTextColor = Color.white;
        public Color characterNameColor = new(0.92f, 0.45f, 0.65f, 1f);
        public Color userNameColor = Color.white;

        [Header("스프라이트 / 폰트 (None이면 GUID에서 자동 로드)")]
        public Sprite bgTextboxCharacter;
        public Sprite bgTextboxUser;
        public Sprite bgNameBoxCharacter;
        public Sprite bgNameBoxUser;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset nameFont;
        public TMP_FontAsset narrationFont;
    }
}
#endif
