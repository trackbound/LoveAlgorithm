using System.Collections.Generic;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스크립트 라인 진행 커서(순수, EventBus·UnityEngine 비의존 — EditMode 테스트). 현재 인덱스 보유 +
    /// LineID 점프 + Choice 블록(뒤따르는 Option 라인) 조회/건너뛰기를 캡슐화한다. 구 ScriptRunner의
    /// currentIndex/lineIndex/collectOptions 책임을 결정 로직만 추려낸 것 — 비동기/연출은 어댑터(NarrativePlayer) 몫.
    /// </summary>
    public sealed class ScriptCursor
    {
        readonly List<ScriptLine> _lines;
        readonly Dictionary<string, int> _index = new();
        int _i;

        public ScriptCursor(List<ScriptLine> lines)
        {
            _lines = lines ?? new List<ScriptLine>();
            for (int j = 0; j < _lines.Count; j++)
            {
                var id = _lines[j].LineID;
                // 첫 등장만 등록(중복 라벨이면 앞선 것이 점프 대상 — 구 BuildLineIndex와 동일).
                if (!string.IsNullOrEmpty(id) && !_index.ContainsKey(id))
                    _index[id] = j;
            }
        }

        /// <summary>현재 인덱스가 유효 범위 안인가(= 진행할 라인이 남았는가).</summary>
        public bool HasCurrent => _i >= 0 && _i < _lines.Count;

        /// <summary>현재 라인(범위 밖이면 null).</summary>
        public ScriptLine Current => HasCurrent ? _lines[_i] : null;

        /// <summary>현재 인덱스(테스트/디버그용).</summary>
        public int Index => _i;

        /// <summary>다음 라인으로 한 칸 전진.</summary>
        public void MoveNext() => _i++;

        /// <summary>LineID로 점프. 성공 시 커서를 그 위치로 옮기고 true.</summary>
        public bool TryJump(string lineId)
        {
            if (!string.IsNullOrEmpty(lineId) && _index.TryGetValue(lineId, out int target))
            {
                _i = target;
                return true;
            }
            return false;
        }

        /// <summary>현재 Choice 라인 바로 뒤에 이어지는 Option 라인들의 Value를 수집(커서 이동 없음).</summary>
        public List<string> PeekOptionValues()
        {
            var result = new List<string>();
            for (int j = _i + 1; j < _lines.Count && _lines[j].Type == LineType.Option; j++)
                result.Add(_lines[j].Value);
            return result;
        }

        /// <summary>현재 Choice 라인 + 뒤따르는 Option 블록을 모두 건너뛴다(점프하지 않은 선택 후 진행).</summary>
        public void SkipChoiceBlock()
        {
            _i++; // Choice 라인 통과
            while (_i < _lines.Count && _lines[_i].Type == LineType.Option) _i++;
        }
    }
}
