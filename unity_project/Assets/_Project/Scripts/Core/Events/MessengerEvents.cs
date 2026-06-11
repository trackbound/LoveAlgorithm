namespace LoveAlgo.Events
{
    /// <summary>
    /// 메신저 시퀀스 도착 명령(EventBus). 발행자(스토리 Flow/일자 자동/디버그)는 시퀀스 id만 알리고,
    /// MessengerController가 카탈로그 해석→상태 기록→통지를 책임진다(ADR-007).
    /// <see cref="OnRead"/>가 실리면 "확인 필수 메시지" — 유저가 해당 시퀀스를 끝까지 읽을 때
    /// 완료된다(스토리 CSV <c>Messenger:{id}:Wait</c>가 대기할 핸들). 미등록 id 등 실패 시에도
    /// 컨트롤러가 핸들을 즉시 완료해 발행측 hang을 막는다(fail-open).
    /// </summary>
    public readonly struct DeliverMessengerSequenceCommand
    {
        public readonly string SequenceId;
        public readonly CompletionHandle OnRead;

        public DeliverMessengerSequenceCommand(string sequenceId, CompletionHandle onRead = null)
        {
            SequenceId = sequenceId;
            OnRead = onRead;
        }
    }

    /// <summary>새 메시지 도착 통지(폰 버튼 진동/배지·채팅 리스트 New 아이콘이 구독).</summary>
    public readonly struct MessengerMessageArrivedEvent
    {
        public readonly string RoomId;
        public readonly string SequenceId;

        public MessengerMessageArrivedEvent(string roomId, string sequenceId)
        {
            RoomId = roomId;
            SequenceId = sequenceId;
        }
    }

    /// <summary>
    /// 메신저 열기 명령(폰 버튼·빠른 메뉴·스토리 공용). <see cref="RoomId"/>가 비면 기본 화면
    /// (친구 탭 — 기획서 "메신저 진입 시 친구 탭에서 기본 시작"), 지정되면 그 방의 채팅창으로 직행.
    /// </summary>
    public readonly struct OpenMessengerCommand
    {
        public readonly string RoomId;

        public OpenMessengerCommand(string roomId = null)
        {
            RoomId = roomId;
        }
    }

    /// <summary>메신저 닫기 명령(공용 뒤로가기/X 버튼).</summary>
    public readonly struct CloseMessengerCommand { }

    /// <summary>
    /// 시퀀스를 끝까지 읽음(모든 선택 응답 포함) 통지. 뷰가 읽음 처리 후 발행하고,
    /// MessengerController가 대기 중인 <see cref="DeliverMessengerSequenceCommand.OnRead"/> 핸들을 완료한다.
    /// </summary>
    public readonly struct MessengerSequenceReadEvent
    {
        public readonly string SequenceId;

        public MessengerSequenceReadEvent(string sequenceId)
        {
            SequenceId = sequenceId;
        }
    }
}
