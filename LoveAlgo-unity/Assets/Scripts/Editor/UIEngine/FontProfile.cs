using UnityEngine;
using TMPro;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// 폰트 프로파일 - 용도별 폰트 정의
    /// </summary>
    [CreateAssetMenu(fileName = "FontProfile", menuName = "LoveAlgo/UI Engine/Font Profile")]
    public class FontProfile : ScriptableObject
    {
        [Header("폰트 에셋")]
        [Tooltip("제목, 캐릭터 이름 등")]
        public TMP_FontAsset titleFont;
        
        [Tooltip("대사, 나레이션")]
        public TMP_FontAsset dialogueFont;
        
        [Tooltip("버튼, 메뉴, 시스템 텍스트")]
        public TMP_FontAsset uiFont;

        [Header("기본 크기")]
        public float titleFontSize = 42f;
        public float dialogueFontSize = 36f;
        public float uiFontSize = 28f;

        [Header("기본 색상")]
        public Color titleColor = Color.white;
        public Color dialogueColor = Color.white;
        public Color uiColor = Color.white;

        /// <summary>
        /// FontTag 타입에 맞는 폰트 반환
        /// </summary>
        public TMP_FontAsset GetFont(FontTag.FontType type)
        {
            return type switch
            {
                FontTag.FontType.Title => titleFont,
                FontTag.FontType.Dialogue => dialogueFont,
                FontTag.FontType.UI => uiFont,
                _ => uiFont
            };
        }

        /// <summary>
        /// FontTag 타입에 맞는 크기 반환
        /// </summary>
        public float GetFontSize(FontTag.FontType type)
        {
            return type switch
            {
                FontTag.FontType.Title => titleFontSize,
                FontTag.FontType.Dialogue => dialogueFontSize,
                FontTag.FontType.UI => uiFontSize,
                _ => uiFontSize
            };
        }

        /// <summary>
        /// FontTag 타입에 맞는 색상 반환
        /// </summary>
        public Color GetColor(FontTag.FontType type)
        {
            return type switch
            {
                FontTag.FontType.Title => titleColor,
                FontTag.FontType.Dialogue => dialogueColor,
                FontTag.FontType.UI => uiColor,
                _ => uiColor
            };
        }
    }
}
