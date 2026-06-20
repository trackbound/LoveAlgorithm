using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LoveAlgo.Events; // ModalButtonKind

namespace LoveAlgo.UI
{
    /// <summary>
    /// 모달 "틀"의 약속. ModalView가 명령의 버튼 종류열로 틀을 고르고(시그니처), 인스턴스화 후 title/message를 채운다.
    /// 정적 틀은 미리 배치된 <see cref="Slots"/>(버튼 스킨 박힘)에 라벨·콜백만 Bind. 폴백 틀(시그니처 빈 배열)은
    /// <see cref="DynamicContainer"/>에 ModalView가 종류별 버튼을 동적 스폰한다.
    /// </summary>
    public class ModalTemplate : MonoBehaviour
    {
        [Tooltip("이 틀이 담당하는 버튼 종류 순서(예: [No,Yes], [Yes]). 빈 배열 = 동적 폴백 틀.")]
        [SerializeField] ModalButtonKind[] signature;
        [Tooltip("제목 TMP(선택, 미바인딩 시 제목 생략).")]
        [SerializeField] TMP_Text title;
        [Tooltip("본문 TMP(선택).")]
        [SerializeField] TMP_Text message;
        [Tooltip("정적 틀: 미리 배치된 버튼 슬롯(좌→우). 폴백이면 비움.")]
        [SerializeField] ChoiceSlot[] slots;
        [Tooltip("폴백 전용: 종류별 버튼을 스폰할 컨테이너. 정적 틀이면 비움.")]
        [SerializeField] Transform dynamicContainer;

        public ModalButtonKind[] Signature { get => signature; set => signature = value; }
        public TMP_Text Title { get => title; set => title = value; }
        public TMP_Text Message { get => message; set => message = value; }
        public ChoiceSlot[] Slots { get => slots; set => slots = value; }
        public Transform DynamicContainer { get => dynamicContainer; set => dynamicContainer = value; }

        /// <summary>정적 틀이면 true(slots 사용), 폴백이면 false(dynamicContainer 사용).</summary>
        public bool IsStatic => slots != null && slots.Length > 0;

        /// <summary>
        /// 명령 종류열 → 선택할 템플릿 인덱스. 정확 매칭(순서·길이 일치) 우선, 없으면 첫 빈-시그니처(폴백) 인덱스,
        /// 둘 다 없으면 -1. GameObject 불필요(EditMode 테스트 대상).
        /// </summary>
        public static int MatchTemplate(IReadOnlyList<ModalButtonKind> commandKinds, IReadOnlyList<ModalButtonKind[]> signatures)
        {
            int fallback = -1;
            for (int t = 0; t < signatures.Count; t++)
            {
                var sig = signatures[t];
                if (sig == null || sig.Length == 0) { if (fallback < 0) fallback = t; continue; } // 폴백 후보
                if (commandKinds == null || sig.Length != commandKinds.Count) continue;
                bool match = true;
                for (int i = 0; i < sig.Length; i++)
                    if (sig[i] != commandKinds[i]) { match = false; break; }
                if (match) return t;
            }
            return fallback;
        }
    }
}
