using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 대사 UI 외부 계약 (Phase B-7c).
    /// 구현: <see cref="LoveAlgo.UI.DialogueUI"/>.
    ///
    /// 외부 호출자 그룹별로 사용 표면이 다르지만 모두 한 DialogueUI 인스턴스를 통과:
    ///   - 라이프사이클: Show/Hide/HideImmediate/RequestShow/Clear/ClearLog/IsHidden/OnHiddenChanged
    ///   - 텍스트 출력: ShowTextAsync + 타이핑 응답(IsTyping/RequestSkip)
    ///   - 로그/Auto: DialogueLog/AddLogEntry/LastDisplayedTextLength (ScriptEngine)
    ///   - 스크립트 콜백: OnEmoteTag (ScriptRunner), ResetMonologueState
    ///   - 설정: SetTextSpeed
    ///
    /// 내부 D9 효과 렌더러/타이핑 리듬/태그 파싱은 캡슐화 — 인터페이스 비노출.
    /// 호출자별 분리 IDialogueLog 도 검토했으나 모두 같은 인스턴스를 받아 동작하므로
    /// 단일 인터페이스 응집이 호출자 종속을 줄임 (ISP 엄격 적용 가치 < 응집 가치).
    /// </summary>
    public interface IDialogueUI
    {
        // ── 상태 ──
        bool IsTyping { get; }
        bool IsHidden { get; }

        /// <summary>마지막 표시된 텍스트 길이 (Auto 딜레이 계산용).</summary>
        int LastDisplayedTextLength { get; }

        /// <summary>대사 로그 (UI 진입점 ShowLogUI 및 ScriptEngine 복원 호출).</summary>
        IReadOnlyList<DialogueLogEntry> DialogueLog { get; }

        // ── 이벤트/콜백 ──
        event Action<bool> OnHiddenChanged;

        /// <summary>인라인 &lt;emote=...&gt; 태그 콜백 (ScriptRunner 가 set).</summary>
        Action<string> OnEmoteTag { get; set; }

        // ── 텍스트 출력 ──
        /// <summary>대사 표시 (타이핑 효과 + 인라인 태그 + D9 시각 효과).</summary>
        UniTask ShowTextAsync(string speaker, string text, CancellationToken ct);

        /// <summary>타이핑 중 클릭/스킵 입력 — 즉시 완성.</summary>
        void RequestSkip();

        // ── 라이프사이클 ──
        void Show();
        void Hide();
        void HideImmediate();

        /// <summary>외부 DialogueShowButton 등에서 호출 — 사용자 hide 해제 + 대사창 복귀.</summary>
        void RequestShow();

        /// <summary>대사 텍스트 + 화자 + 일부 상태 초기화.</summary>
        void Clear();

        /// <summary>대사 로그 비우기 (세션 시작 시).</summary>
        void ClearLog();

        /// <summary>스크립트 엔진 로그 복원용 — 로그 항목 직접 추가.</summary>
        void AddLogEntry(string speaker, string text);

        /// <summary>독백/모놀로그 상태 리셋 (스크립트 점프 시 잔재 방지).</summary>
        void ResetMonologueState();

        // ── 설정 ──
        /// <summary>타이핑 속도 (0=느림, 1=빠름 — 정규화된 슬라이더 값). Settings UI 에서 호출.</summary>
        void SetTextSpeed(float normalized);
    }
}
