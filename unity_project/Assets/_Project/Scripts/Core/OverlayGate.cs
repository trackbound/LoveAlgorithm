using System;
using System.Collections.Generic;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 블로킹 오버레이(설정/세이브로드 등 메뉴 팝업) 스택 게이트(<see cref="NarrativeFlowGate"/> 형제,
    /// ADR-013 Overlay 보조). 역할 둘:
    /// ① 오버레이가 떠 있는 동안 게임플레이 입력 차단(<see cref="IsBlocked"/> — DialogueView 진행/오토,
    ///    ScheduleView 선택이 확인)
    /// ② 공용 뒤로가기의 "가장 마지막에 출력된 팝업 닫기"(<see cref="CloseTop"/> — 서브메뉴 기획서 규칙)
    ///
    /// 사용: 표시 시 <c>_gate = OverlayGate.Push(() => SetVisible(false))</c>, 숨김 시 <c>_gate.Dispose()</c>.
    /// 토큰 방식이라 위에 다른 팝업이 떠 있어도(비-LIFO 강제 종료) 자기 항목만 정확히 제거된다 —
    /// 깊이 카운터로는 뒤로가기가 누가 최상단인지 알 수 없어 스택으로 확장했다.
    ///
    /// <para>왜 필요한가(입력 차단): 마우스 클릭은 오버레이의 <c>blocksRaycasts</c>가 막지만, DialogueView가
    /// 키보드(스페이스)를 <c>Update</c>에서 직접 읽고 오토 진행이 타이머라 raycast로 안 막힌다. 정적이라
    /// 도메인 리로드 시 비워지고, 씬 전환/비정상 대비 <see cref="Reset"/> 제공(뷰 OnDisable 누수가드와 이중 안전).</para>
    /// </summary>
    public static class OverlayGate
    {
        sealed class Entry : IDisposable
        {
            readonly Action _requestClose;
            public Entry(Action requestClose) { _requestClose = requestClose; }

            /// <summary>닫기 요청 — 액션이 없으면(뒤로가기로 닫을 수 없는 오버레이) false.</summary>
            public bool TryRequestClose()
            {
                if (_requestClose == null) return false;
                _requestClose();
                return true;
            }

            public void Dispose() => _stack.Remove(this); // 중복 Dispose/Reset 후 무해(Remove no-op)
        }

        static readonly List<Entry> _stack = new();

        /// <summary>블로킹 오버레이가 떠 있는가(게임플레이 입력 차단 신호).</summary>
        public static bool IsBlocked => _stack.Count > 0;

        /// <summary>떠 있는 오버레이 수(테스트/디버그).</summary>
        public static int Count => _stack.Count;

        /// <summary>
        /// 오버레이 표시 등록. 반환 토큰을 숨김 시 Dispose(중복 무해).
        /// <paramref name="requestClose"/> = 뒤로가기(<see cref="CloseTop"/>)가 이 오버레이를 닫을 때 호출할
        /// 액션(보통 자기 SetVisible(false)). null이면 뒤로가기로 닫히지 않는 오버레이(차단만).
        /// </summary>
        public static IDisposable Push(Action requestClose = null)
        {
            var e = new Entry(requestClose);
            _stack.Add(e);
            return e;
        }

        /// <summary>
        /// 가장 마지막에 출력된(최상단) 오버레이에 닫기를 요청한다(공용 뒤로가기). 엄격히 최상단만 —
        /// 최상단이 닫기 불가(null 액션)면 아래를 건너 닫지 않는다(가려진 팝업이 닫히는 오동작 방지).
        /// 실제 스택 제거는 해당 뷰가 숨겨지며 토큰을 Dispose할 때 일어난다(상태 비동기화 방지).
        /// </summary>
        /// <returns>닫기를 요청했으면 true, 닫을 팝업이 없거나 최상단이 닫기 불가면 false.</returns>
        public static bool CloseTop()
        {
            if (_stack.Count == 0) return false;
            return _stack[^1].TryRequestClose();
        }

        /// <summary>강제 전체 해제(씬 전환/비정상 안전망 — 일반 흐름은 Push/Dispose 짝).</summary>
        public static void Reset() => _stack.Clear();
    }
}
