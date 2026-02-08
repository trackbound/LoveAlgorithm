using UnityEngine;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// TMP_Text에 붙여서 폰트 용도를 지정하는 태그
    /// UI Manager에서 이 태그를 기준으로 폰트를 일괄 적용
    /// </summary>
    [DisallowMultipleComponent]
    public class FontTag : MonoBehaviour
    {
        public enum FontType
        {
            /// <summary>제목, 캐릭터 이름</summary>
            Title,
            /// <summary>대사, 나레이션</summary>
            Dialogue,
            /// <summary>버튼, 메뉴, 시스템</summary>
            UI
        }

        [Tooltip("이 텍스트의 용도 (폰트 프로파일에서 해당 폰트 적용)")]
        public FontType fontType = FontType.UI;

        [Tooltip("프로파일 적용 시 이 항목은 건너뜀")]
        public bool ignoreProfileApplication = false;
    }
}
