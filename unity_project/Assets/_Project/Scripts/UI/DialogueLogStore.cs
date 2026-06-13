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

    /// <summary>로그 한 박스 — 같은 스크립트 내 연속 동일 화자의 줄들이 누적된다(목업 그룹핑 규칙).</summary>
    public sealed class DialogueLogEntry
    {
        public DialogueLogKind Kind;
        public string Speaker;   // 표시명(치환 후 — 플레이어는 입력 이름)
        public string SpeakerId; // 캐릭터 코드(c01~) | "player" | null
        public string ScriptId;  // 그룹핑 경계(같은 스크립트)
        public readonly List<string> Lines = new();
    }

    /// <summary>
    /// 대사 로그 저장소(정적 — OverlayGate 형제). 세이브 비영속·런타임 누적(감독 승인: 영속은 후속 슬라이스),
    /// 부팅 시 <see cref="DialogueLogRecorder"/>가 리셋. 그룹핑 규칙(목업 주석 동결):
    /// 같은 스크립트 + 연속 동일 화자(독백은 화자 무관 연속) = 한 박스에 줄 누적, 그 외 = 새 박스.
    /// 순수 자료구조라 EditMode 테스트 대상.
    /// </summary>
    public static class DialogueLogStore
    {
        static readonly List<DialogueLogEntry> _entries = new();

        public static IReadOnlyList<DialogueLogEntry> Entries => _entries;
        public static int Count => _entries.Count;

        public static void Reset() => _entries.Clear();

        /// <summary>대사 1줄 적재 — 그룹핑 규칙에 따라 마지막 박스에 병합하거나 새 박스를 연다.</summary>
        public static void Append(string scriptId, string speaker, string speakerId, string text)
        {
            var kind = KindOf(speaker, speakerId);
            var last = _entries.Count > 0 ? _entries[_entries.Count - 1] : null;
            bool merge = last != null
                && last.Kind == kind
                && last.ScriptId == scriptId
                && (kind == DialogueLogKind.Narration || last.Speaker == speaker);

            if (!merge)
            {
                last = new DialogueLogEntry
                {
                    Kind = kind,
                    Speaker = speaker ?? "",
                    SpeakerId = speakerId,
                    ScriptId = scriptId,
                };
                _entries.Add(last);
            }
            last.Lines.Add(text ?? "");
        }

        static DialogueLogKind KindOf(string speaker, string speakerId)
        {
            if (speakerId == PlayerNameFormat.PlayerSpeakerId) return DialogueLogKind.Player;
            if (string.IsNullOrEmpty(speaker)) return DialogueLogKind.Narration;
            return DialogueLogKind.Character;
        }
    }
}
