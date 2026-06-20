using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 효과음 등록 테이블(ADR-012: 매직 문자열 분산 금지). 상호작용별 SFX "클립 이름"을 한 곳에 모은다.
    /// 트리거 지점(StyledButton·DialogueView·ButtonSlot)이 이 SO를 읽어 이름을 PlaySfxCommand로 발행하고,
    /// 실제 재생은 AudioManager가 담당한다(ADR-007: 재생 싱크 단일). 런타임 읽기 전용.
    ///
    /// <para>이름 규약 = AudioManager의 SFX 로딩 경로 Resources/Audio/SFX/{name}.
    /// 빈 문자열 = 무음(트리거 지점이 발행 전 IsNullOrEmpty로 가드 → "SFX 없음" 경고 스팸 방지).</para>
    ///
    /// 에셋: Resources/Data/UiSound.asset (트리거 지점이 부팅 시 1회 로드해 공유).
    /// </summary>
    [CreateAssetMenu(fileName = "UiSound", menuName = "LoveAlgo/UI Sound")]
    public class UiSoundSO : ScriptableObject
    {
        [Header("일반 UI 버튼 (모달 등)")]
        [Tooltip("버튼 호버 시 SFX 이름(Resources/Audio/SFX/). 비우면 무음.")]
        [SerializeField] string buttonHover = "";
        [Tooltip("버튼 클릭 시 SFX 이름. 비우면 무음.")]
        [SerializeField] string buttonClick = "";

        [Header("선택지 전용")]
        [Tooltip("선택지 호버 시 SFX 이름. 비우면 무음.")]
        [SerializeField] string choiceHover = "";
        [Tooltip("선택지 클릭 시 SFX 이름. 비우면 무음.")]
        [SerializeField] string choiceClick = "";

        [Header("대사 진행")]
        [Tooltip("대사 한 줄을 완성한 뒤 '다음'으로 넘어갈 때 SFX 이름(타이핑 스킵=가속 시에는 재생 안 함). 비우면 무음.")]
        [SerializeField] string dialogueAdvance = "";
        [Tooltip("타이핑 한 글자 블립 SFX 이름. 비우면 무음.")]
        [SerializeField] string dialogueType = "";

        [Header("타이핑 스로틀")]
        [Tooltip("타이핑 블립을 N글자마다 1회만 재생(per-char 기관총 방지). 최소 1.")]
        [SerializeField] int typeStride = 2;

        public string ButtonHover => buttonHover;
        public string ButtonClick => buttonClick;
        public string ChoiceHover => choiceHover;
        public string ChoiceClick => choiceClick;
        public string DialogueAdvance => dialogueAdvance;
        public string DialogueType => dialogueType;

        /// <summary>타이핑 블립 재생 간격(글자 수). 0/음수 방어로 최소 1.</summary>
        public int TypeStride => Mathf.Max(1, typeStride);

        // 공유 인스턴스: 트리거 지점(StyledButton·DialogueView 등)이 Resources에서 1회 로드해 공유.
        static UiSoundSO _shared;
        static bool _sharedLoaded;

        /// <summary>공유 등록 테이블(Resources/Data/UiSound). 에셋 부재 시 null = 전부 무음.</summary>
        public static UiSoundSO Shared
        {
            get
            {
                if (!_sharedLoaded)
                {
                    _shared = Resources.Load<UiSoundSO>("Data/UiSound");
                    _sharedLoaded = true;
                }
                return _shared;
            }
        }
    }
}
