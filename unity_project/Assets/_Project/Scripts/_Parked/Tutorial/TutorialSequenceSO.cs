using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Tutorial
{
    /// <summary>로아 안내 아이콘 표정(기획서 'new 작업 필요 목록' 3~4종 — 아트 도착 시 스프라이트 연결).</summary>
    public enum RoaTutorialEmote
    {
        Basic,  // 로아기본
        Smile,  // 로아눈웃음
        Bright, // 로아밝게웃음
        Beam    // 로아활짝
    }

    /// <summary>
    /// 튜토리얼 시퀀스 정의(Definition) — 스탯/자유행동 첫 진입 안내(기획서 내부 콘텐츠 p7~34).
    /// 스텝 대사·하이라이트·클릭 제한을 데이터로 보유 — 기획 수정은 이 에셋에서(시드 생성 = Tools 메뉴).
    /// 하이라이트/클릭 대상은 씬 오브젝트가 아닌 **앵커 id 문자열**로 추상화 — 씬엔 TutorialAnchor(id)만
    /// 붙이면 되므로 병렬 UI 재작업과 충돌하지 않는다(합류 후 부착).
    /// 진행 1회 기록은 설치 단위 PlayerPrefs(<see cref="prefsKey"/>) — 세이브 스키마 무접촉(기획:
    /// "새 게임 시작 또는 두 번째 플레이부터는 출력 X" = FirstLaunchFlag 선례).
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialSequence", menuName = "LoveAlgo/Tutorial Sequence")]
    public class TutorialSequenceSO : ScriptableObject
    {
        [System.Serializable]
        public class Step
        {
            [Tooltip("로아 말풍선 텍스트. {{Player}} = 플레이어 이름 치환(스토리 토큰 선례).")]
            [TextArea(2, 5)] public string text;
            [Tooltip("로아 아이콘 표정.")]
            public RoaTutorialEmote emote = RoaTutorialEmote.Basic;
            [Tooltip("하이라이트 앵커 id(빈 값 = 중앙 표시, 하이라이트 없음). 예: LeftPanel, StatPanel, ShopButton.")]
            public string highlightAnchor = "";
            [Tooltip("이 앵커를 클릭해야만 진행(빈 값 = 아무 클릭으로 진행). 예: ShopButton, ShopBack — 기획 '그냥 넘어가기 안됨'.")]
            public string requiredClickAnchor = "";
            [Tooltip("스텝 표시 전 지연(초). 기획: 첫 스텝은 진입 2초 후.")]
            [Min(0f)] public float appearDelay;
            [Tooltip("0보다 크면 클릭 없이 n초 후 자동 진행(기획: 마지막 스텝 4초 뒤 사라짐).")]
            [Min(0f)] public float autoAdvanceSeconds;
            [Tooltip("이 스텝의 딤 텍스처(디자이너 제작 — 영역별 구멍 베이크, Art/스케줄 튜토리얼). 비우면 풀딤 폴백.")]
            public Sprite dim;
            [Tooltip("로아 아이콘+말풍선 그룹의 anchoredPosition(화면 중앙 기준). 기획: 스텝마다 위치 이동.")]
            public Vector2 roaPosition;
        }

        [Tooltip("설치 단위 1회 기록 키(PlayerPrefs). 비우면 항상 재생(데브).")]
        public string prefsKey = "Tutorial_ScheduleIntro";

        [SerializeField] List<Step> steps = new();

        public IReadOnlyList<Step> Steps => steps;

        /// <summary>스텝 일괄 설정(시드 생성기/테스트 주입용 — 인스펙터 편집과 동치).</summary>
        public void SetSteps(List<Step> value) => steps = value ?? new List<Step>();
    }
}
