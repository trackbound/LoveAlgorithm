using UnityEngine;
using LoveAlgo.Contracts;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// Mark 명령 — 씬 경계 마커. CSV: `,Flow,,Mark:label,>`
    ///
    /// 실행 시점에는 **no-op** (아무 일도 안 함). 합성기·점프 시스템 전용 메타데이터.
    ///
    /// 시맨틱 약속:
    ///   "Mark 라인 시점에 무대(BG/Char/CG/SD/Overlay/BGM)가 깨끗(empty)하다."
    ///   → 보통 한 씬이 끝나 BG 전환 + 모든 캐릭터 Exit 직후에 박는다.
    ///
    /// 용도:
    ///   1) StageStateSynthesizer가 점프 시 가장 가까운 Mark까지만 역추적 → 짧고 정확한 합성
    ///   2) MarkRegistry가 라벨→인덱스 등록 → 디버그 점프 메뉴 자동 생성
    ///   3) (선택) ScriptLine.LineID에 MARK_label 자동 부여 → JumpTo:MARK_label도 가능
    /// </summary>
    public static class MarkFlowCommand
    {
        /// <summary>Mark:label에서 label만 추출. 라벨 없으면 빈 문자열.</summary>
        public static string ExtractLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            int colon = value.IndexOf(':');
            if (colon < 0 || colon >= value.Length - 1) return "";
            return value.Substring(colon + 1);
        }

        /// <summary>실행 — no-op. 디버그 가시성 위해 로그만.</summary>
        public static void Execute(string[] parts)
        {
            string label = parts != null && parts.Length >= 2 ? parts[1] : "";
            Debug.Log($"[Flow] Mark passed: {label}");
        }
    }
}
