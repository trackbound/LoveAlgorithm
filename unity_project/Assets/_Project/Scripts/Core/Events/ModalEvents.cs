using System;
using System.Collections.Generic;

namespace LoveAlgo.Events
{
    // ── 범용 모달 명령/완료 핸들(시스템 UI — 확인/알림 다이얼로그) ──
    // 코드(타이틀 종료 확인 등)가 ShowModalCommand를 발행 → ModalView가 띄우고, 사용자가 버튼을 누르면
    // ModalRequest에 인덱스를 채운다. 스토리 선택지(ChoiceView)와 별개 — 그건 작가 CSV용, 이건 C# 시스템 UI.
    // 분해는 vn_conventions §7(ScreenFade 템플릿)·ChoiceRequest 형제 패턴을 따른다(ADR-007: 표시는 뷰, 의미는 호출부).

    /// <summary>
    /// 범용 모달 완료 핸들(<see cref="ChoiceRequest"/> 형제 — 값 회수형). 뷰가 사용자가 누른 버튼 인덱스를
    /// <see cref="Select"/>로 채우면, 호출부는 (a) 생성 시 넘긴 <c>onSelected</c> 콜백으로 즉시 받거나
    /// (b) 코루틴에서 <see cref="IsComplete"/>를 폴링한다. 시스템 UI(코드 호출)가 주 소비처라 콜백을 1급으로
    /// 둔다 — 호출부가 코루틴을 돌리지 않아도 결과 분기가 가능(예: 종료 확인). 첫 선택만 유효(중복·음수 무시)라
    /// 콜백 1회를 보장한다. <see cref="CompletionHandle"/>은 "완료"만, 이쪽은 "어느 버튼"까지 회수.
    /// </summary>
    public sealed class ModalRequest
    {
        readonly Action<int> _onSelected;

        public int SelectedIndex { get; private set; } = -1;
        public bool IsComplete => SelectedIndex >= 0;

        /// <param name="onSelected">버튼 선택 시 즉시 호출(인덱스 전달). null이면 폴링 전용.</param>
        public ModalRequest(Action<int> onSelected = null) { _onSelected = onSelected; }

        /// <summary>버튼 i를 선택. 첫 호출만 유효(이후·음수는 무시) → 콜백 1회 보장.</summary>
        public void Select(int index)
        {
            if (index < 0 || IsComplete) return;
            SelectedIndex = index;
            _onSelected?.Invoke(index);
        }
    }

    /// <summary>
    /// 범용 모달 표시 명령. <see cref="ButtonLabels"/> 순서대로 버튼을 만들고, 클릭 시 <see cref="Handle"/>에
    /// 인덱스를 채운다(ADR-007: 표시만, 의미 해석은 호출부). 메시지+버튼만 — 텍스트 입력은 후속(입력은 LockScreen
    /// 담당). 스토리 선택지가 아니라 코드에서 발행하는 시스템 모달이다. 핸들은 참조형 클래스라 readonly struct
    /// 명령에 안전하게 실린다(<see cref="ShowChoiceCommand"/>와 동일 관용).
    /// </summary>
    public readonly struct ShowModalCommand
    {
        public readonly string Title;
        public readonly string Message;
        public readonly IReadOnlyList<string> ButtonLabels;
        public readonly ModalRequest Handle;

        public ShowModalCommand(string title, string message, IReadOnlyList<string> buttonLabels, ModalRequest handle)
        {
            Title = title;
            Message = message;
            ButtonLabels = buttonLabels;
            Handle = handle;
        }
    }
}
