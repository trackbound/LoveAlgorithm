using UnityEngine;
using TMPro;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// 폰트 프로파일 — 5가지 용도별 폰트·크기·색상 정의
    /// 
    /// Header   → Aggro Bold          (팝업 제목)
    /// Label    → Aggro Medium        (카테고리, 항목, 버튼, 이벤트/장소명)
    /// Caption  → Aggro Light         (설명, 이름, 선택지, 독백)
    /// Dialogue → Pretendard SemiBold (인게임 대사)
    /// Body     → Pretendard Medium   (로그 대사, 입력 필드)
    /// </summary>
    [CreateAssetMenu(fileName = "FontProfile", menuName = "LoveAlgo/UI Engine/Font Profile")]
    public class FontProfile : ScriptableObject
    {
        [Header("Header — 팝업 제목 (Aggro Bold)")]
        public TMP_FontAsset headerFont;
        public float headerFontSize = 48f;
        public Color headerColor = Color.white;

        [Header("Label — 카테고리/항목/버튼 (Aggro Medium)")]
        public TMP_FontAsset labelFont;
        public float labelFontSize = 32f;
        public Color labelColor = Color.white;

        [Header("Caption — 설명/선택지/독백 (Aggro Light)")]
        public TMP_FontAsset captionFont;
        public float captionFontSize = 28f;
        public Color captionColor = Color.white;

        [Header("Dialogue — 인게임 대사 (Pretendard SemiBold)")]
        public TMP_FontAsset dialogueFont;
        public float dialogueFontSize = 36f;
        public Color dialogueColor = Color.white;

        [Header("Body — 로그 대사/입력 (Pretendard Medium)")]
        public TMP_FontAsset bodyFont;
        public float bodyFontSize = 28f;
        public Color bodyColor = Color.white;

        /// <summary>
        /// FontTag 타입에 맞는 폰트 반환
        /// </summary>
        public TMP_FontAsset GetFont(FontTag.FontType type)
        {
            return type switch
            {
                FontTag.FontType.Header => headerFont,
                FontTag.FontType.Label => labelFont,
                FontTag.FontType.Caption => captionFont,
                FontTag.FontType.Dialogue => dialogueFont,
                FontTag.FontType.Body => bodyFont,
                _ => labelFont
            };
        }

        /// <summary>
        /// FontTag 타입에 맞는 크기 반환
        /// </summary>
        public float GetFontSize(FontTag.FontType type)
        {
            return type switch
            {
                FontTag.FontType.Header => headerFontSize,
                FontTag.FontType.Label => labelFontSize,
                FontTag.FontType.Caption => captionFontSize,
                FontTag.FontType.Dialogue => dialogueFontSize,
                FontTag.FontType.Body => bodyFontSize,
                _ => labelFontSize
            };
        }

        /// <summary>
        /// FontTag 타입에 맞는 색상 반환
        /// </summary>
        public Color GetColor(FontTag.FontType type)
        {
            return type switch
            {
                FontTag.FontType.Header => headerColor,
                FontTag.FontType.Label => labelColor,
                FontTag.FontType.Caption => captionColor,
                FontTag.FontType.Dialogue => dialogueColor,
                FontTag.FontType.Body => bodyColor,
                _ => labelColor
            };
        }
    }
}
