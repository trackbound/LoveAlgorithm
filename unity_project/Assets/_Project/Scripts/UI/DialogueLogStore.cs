using System.Collections.Generic;
using LoveAlgo.Core; // PlayerNameFormat

namespace LoveAlgo.UI
{
    /// <summary>로그 엔트리 종류 — 렌더 변형(슬롯 프리팹) 선택 키.</summary>
    public enum DialogueLogKind
    {
        Character, // 히로인/엑스트라: namebox_character+textbox_character(검정 본문). 초상은 뷰의 id→스프라이트 매핑(미등록=초상 없음)
        Player,    // 주인공: namebox_player+textbox_player(흰 본문)
        Narration  // 독백(화자 빈 칸): 박스 없는 흰 텍스트
    }

    /// <summary>로그 한 박스 — 대사 한 줄(= 한 번의 진행 <c>ShowDialogueCommand</c>)에 1:1 대응한다.
    /// 한 박스 안의 여러 시각 줄은 <see cref="Text"/> 본문의 <c>\n</c>(한 CSV 행이 담은 줄바꿈)으로 표현된다.</summary>
    public sealed class DialogueLogEntry
    {
        public DialogueLogKind Kind;
        public string Speaker;   // 표시명(치환 후 — 플레이어는 입력 이름)
        public string SpeakerId; // 캐릭터 코드(c01~) | "player" | null
        public string Text;      // 본문(한 진행 = 한 박스 — \n은 같은 박스 안 여러 줄)
    }

    /// <summary>
    /// 대사 로그 저장소(정적 — OverlayGate 형제). 세이브 비영속·런타임 누적(감독 승인: 영속은 후속 슬라이스),
    /// 부팅 시 <see cref="DialogueLogRecorder"/>가 리셋. 그룹핑 규칙(목업 동결): 진행(대사 한 줄) 단위로 박스가
    /// 나뉜다 — 같은 화자가 연속으로 말해도 다음 진행이면 새 박스(목업 박스2: "다음 스크립트에서 한 인물이
    /// 계속해서 얘기하면 새로운 박스가 생깁니다"). 한 박스에 여러 줄이 보이는 경우(목업 박스1)는 한 CSV 행의
    /// 본문이 <c>\n</c>로 줄을 담은 것 — 행 병합이 아니다. 순수 자료구조라 EditMode 테스트 대상.
    /// </summary>
    public static class DialogueLogStore
    {
        static readonly List<DialogueLogEntry> _entries = new();

        public static IReadOnlyList<DialogueLogEntry> Entries => _entries;
        public static int Count => _entries.Count;

        public static void Reset() => _entries.Clear();

        /// <summary>대사 한 줄(한 진행) 적재 — 항상 새 박스를 연다(진행 단위 분리).</summary>
        public static void Append(string speaker, string speakerId, string text)
        {
            _entries.Add(new DialogueLogEntry
            {
                Kind = KindOf(speaker, speakerId),
                Speaker = speaker ?? "",
                SpeakerId = speakerId,
                Text = text ?? "",
            });
        }

        static DialogueLogKind KindOf(string speaker, string speakerId)
        {
            if (speakerId == PlayerNameFormat.PlayerSpeakerId) return DialogueLogKind.Player;
            if (string.IsNullOrEmpty(speaker)) return DialogueLogKind.Narration;
            return DialogueLogKind.Character;
        }

        /// <summary>두 박스가 같은 화자인지 — 연속 동일 화자 구간(run)에서 이름표/초상을 첫 박스에만 표시하기 위한 판정.
        /// 종류가 같고 화자 키(SpeakerId 우선, 없으면 표시명)가 같으면 동일 화자(엑스트라는 이름으로 구분).</summary>
        public static bool IsSameSpeaker(DialogueLogEntry a, DialogueLogEntry b)
        {
            if (a == null || b == null || a.Kind != b.Kind) return false;
            string ka = string.IsNullOrEmpty(a.SpeakerId) ? a.Speaker : a.SpeakerId;
            string kb = string.IsNullOrEmpty(b.SpeakerId) ? b.Speaker : b.SpeakerId;
            return ka == kb;
        }
    }
}
