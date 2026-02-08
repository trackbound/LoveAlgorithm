using UnityEngine;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// TMP_Text에 붙여서 폰트 용도를 지정하는 태그
    /// UI Manager에서 이 태그를 기준으로 폰트를 일괄 적용
    /// 
    /// 폰트 매핑:
    ///   Header   → Aggro Bold       (팝업 제목)
    ///   Label    → Aggro Medium     (카테고리, 설정 항목, 버튼, 이벤트/장소명, LOG, 로그 이름)
    ///   Caption  → Aggro Light      (설명, 대사창 이름, 선택지, 팝업 텍스트/버튼, 독백)
    ///   Dialogue → Pretendard SemiBold  (인게임 대사)
    ///   Body     → Pretendard Medium    (로그 대사, 이름 입력 칸)
    /// </summary>
    [DisallowMultipleComponent]
    public class FontTag : MonoBehaviour
    {
        public enum FontType
        {
            /// <summary>팝업 제목 — Aggro Bold</summary>
            Header = 0,
            /// <summary>카테고리, 설정 항목, 버튼, 이벤트/장소명, LOG, 로그 이름 — Aggro Medium</summary>
            Label = 1,
            /// <summary>설명, 대사창 이름, 선택지, 팝업 텍스트/버튼, 독백 — Aggro Light</summary>
            Caption = 2,
            /// <summary>인게임 대사 — Pretendard SemiBold</summary>
            Dialogue = 3,
            /// <summary>로그 대사, 이름 입력 칸 — Pretendard Medium</summary>
            Body = 4,
        }

        [Tooltip("이 텍스트의 용도 (폰트 프로파일에서 해당 폰트 적용)")]
        public FontType fontType = FontType.Label;

        [Tooltip("프로파일 적용 시 이 항목은 건너뜀")]
        public bool ignoreProfileApplication = false;
    }
}
