using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 현재 로드된 스크립트의 Mark 라벨 → 라인 인덱스 인덱스.
    /// 스크립트 로드 시 ScriptRunner가 Rebuild 호출.
    ///
    /// 용도:
    ///   - 디버그 점프 메뉴 자동 생성 (라벨로 사람 친화적 점프)
    ///   - StageStateSynthesizer가 가까운 Mark 역방향 탐색 시 O(n) 스캔 (현재) 또는 정렬된 인덱스 활용 (옵션)
    /// </summary>
    public static class MarkRegistry
    {
        // 라벨 → 인덱스
        static readonly Dictionary<string, int> _labelToIndex = new Dictionary<string, int>();
        // 인덱스 오름차순 정렬된 (index, label) — 역방향 탐색 가속용
        static readonly List<(int index, string label)> _sortedByIndex = new List<(int, string)>();

        public static int Count => _sortedByIndex.Count;
        public static IReadOnlyList<(int index, string label)> All => _sortedByIndex;

        public static bool TryGetIndex(string label, out int index)
            => _labelToIndex.TryGetValue(label ?? "", out index);

        // ── Scene Mark 카테고리 (Mark:scene:…) ──────────────────────────────
        // 시맨틱: "여기는 한 씬의 시작점. 직후에 Setup FX 라인이 모든 상태를 명시."
        // 점프 시 가장 신뢰할 수 있는 anchor (합성기 부담 X, Setup으로 정확).

        const string SceneCategoryPrefix = "scene:";

        public static bool IsSceneMark(string label)
            => !string.IsNullOrEmpty(label)
            && label.StartsWith(SceneCategoryPrefix, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Scene Mark 라벨에서 표시용 이름 추출 ("scene:로아 인트로" → "로아 인트로").</summary>
        public static string GetSceneDisplayName(string label)
        {
            if (!IsSceneMark(label)) return label ?? "";
            return label.Substring(SceneCategoryPrefix.Length).Trim();
        }

        /// <summary>주어진 라인 인덱스 이하 가장 가까운 Scene Mark의 인덱스. 없으면 -1.</summary>
        public static int FindNearestSceneMarkAtOrBefore(int targetIndex)
        {
            int best = -1;
            for (int i = 0; i < _sortedByIndex.Count; i++)
            {
                var (idx, label) = _sortedByIndex[i];
                if (idx > targetIndex) break;
                if (IsSceneMark(label)) best = idx;
            }
            return best;
        }

        /// <summary>주어진 라인 인덱스 이하 가장 가까운 Mark의 인덱스. 없으면 -1.</summary>
        public static int FindNearestMarkAtOrBefore(int targetIndex)
        {
            // 정렬되어 있으므로 이진 탐색이 이상적이지만, Mark는 보통 수십 개 → 선형으로 충분
            int best = -1;
            for (int i = 0; i < _sortedByIndex.Count; i++)
            {
                int idx = _sortedByIndex[i].index;
                if (idx <= targetIndex) best = idx;
                else break; // 정렬되어 있으므로 더 볼 필요 없음
            }
            return best;
        }

        /// <summary>스크립트 로드 후 호출. 모든 Mark 라인을 스캔해서 등록.</summary>
        public static void Rebuild(IReadOnlyList<ScriptLine> lines)
        {
            _labelToIndex.Clear();
            _sortedByIndex.Clear();

            if (lines == null) return;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || line.Type != LineType.Flow) continue;
                if (string.IsNullOrEmpty(line.Value)) continue;
                if (!line.Value.StartsWith("Mark", System.StringComparison.OrdinalIgnoreCase)) continue;

                string label = StoryEngine.Flow.MarkFlowCommand.ExtractLabel(line.Value);
                if (string.IsNullOrEmpty(label))
                {
                    Debug.LogWarning($"[MarkRegistry] line {line.SourceLine}: Mark에 라벨이 없음 → 등록 스킵");
                    continue;
                }
                if (_labelToIndex.ContainsKey(label))
                {
                    Debug.LogWarning($"[MarkRegistry] 중복 라벨 '{label}' (line {line.SourceLine}) — 첫 것 유지, 무시");
                    continue;
                }
                _labelToIndex[label] = i;
                _sortedByIndex.Add((i, label));
            }

            _sortedByIndex.Sort((a, b) => a.index.CompareTo(b.index));
            Debug.Log($"[MarkRegistry] 등록 완료: {_sortedByIndex.Count}개");
        }

        public static void Clear()
        {
            _labelToIndex.Clear();
            _sortedByIndex.Clear();
        }
    }
}
