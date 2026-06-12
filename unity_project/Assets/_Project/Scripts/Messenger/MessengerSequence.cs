using System.Collections.Generic;

namespace LoveAlgo.Messenger
{
    /// <summary>메신저 시퀀스 한 줄의 종류 — 말풍선 2종 + 선택지 그룹.</summary>
    public enum MessengerLineKind
    {
        Message,   // 상대 말풍선 (Msg 행)
        MyMessage, // 주인공 말풍선 (Me 행)
        Choice     // 선택지 그룹 (연속 Option 행 묶음)
    }

    /// <summary>
    /// 시퀀스 한 줄(말풍선 1개 또는 선택지 그룹 1개). 메신저는 선형 진행(점프 없음) —
    /// 선택은 "내 말풍선이 무엇이 되는가 + 효과"만 가르고 다음 줄로 이어진다(기획서 채팅창 사양).
    /// </summary>
    public sealed class MessengerLine
    {
        public MessengerLineKind Kind;
        public string SenderId = "";              // Message만 사용(Msg 행 Speaker 칸)
        public string Text = "";                  // Message/MyMessage만 사용
        public List<MessengerOption> Options;     // Choice만 사용
    }

    /// <summary>
    /// 선택지 1개. 셀 문법은 스토리 Option에서 점프 슬롯만 뺀 형태(메신저는 분기 없음):
    /// <c>버튼텍스트|효과1|효과2|...|if:조건</c>. 효과 문자열은 스토리 선택지와 동일 문법
    /// (예: <c>Love:Roa:1</c> — Love의 히로인 id는 호감도 정본 id(Roa/HaYeEun/...)로, 발신자
    /// c0X(에셋/친구 id)와 다른 공간) — 해석·발행은 어댑터 몫이라 여기선 원문 보관.
    /// </summary>
    public sealed class MessengerOption
    {
        public string Text = "";
        public readonly List<string> Effects = new();
        public string Condition; // if: 조건(스토리 Option·Flow If와 동일 문법). null=항상 표시.
    }

    /// <summary>파싱 결과 — 순수(로깅 없음). 오류는 수집해 반환하고 유효한 줄만 담는다.</summary>
    public sealed class MessengerParseResult
    {
        public readonly List<MessengerLine> Lines = new();
        public readonly List<string> Errors = new();
        public bool HasErrors => Errors.Count > 0;
    }
}
