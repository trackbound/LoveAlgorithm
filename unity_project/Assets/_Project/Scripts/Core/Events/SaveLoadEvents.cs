namespace LoveAlgo.Events
{
    // ── 세이브/로드 팝업 커맨드 ──
    // SaveLoadView가 ShowSaveLoadCommand로 표시(ADR-013 Overlay), 슬롯 클릭 시 Load=LoadGameCommand / Save=기존
    // SaveRequestedEvent 발행. 저장 시 썸네일은 CaptureThumbnailCommand로 인게임 컨트롤러가 캡처(ADR-007).

    /// <summary>세이브/로드 팝업 모드. Title=Load 전용, 인게임=Save/Load.</summary>
    public enum SaveLoadMode { Save, Load }

    /// <summary>세이브/로드 팝업 열기. SaveLoadView가 구독해 표시(완료-핸들 불요 — 단순 show/hide Overlay).</summary>
    public readonly struct ShowSaveLoadCommand
    {
        public readonly SaveLoadMode Mode;
        public ShowSaveLoadCommand(SaveLoadMode mode) { Mode = mode; }
    }

    /// <summary>특정 슬롯 로드(<c>ContinueGameCommand</c> 형제 — 그건 자동저장 슬롯0 전용). SceneFlowController가
    /// 받아 부팅 모드+슬롯을 설정하고 Game 씬을 로드한다.</summary>
    public readonly struct LoadGameCommand
    {
        public readonly int Slot;
        public LoadGameCommand(int slot) { Slot = slot; }
    }

    /// <summary>저장 시 썸네일 캡처 요청. 인게임 ThumbnailCaptureController가 구독해 UI 배제 캡처 후 슬롯 PNG를 기록.
    /// (SaveData.thumbnailFile은 저장 시 세팅, PNG는 이 명령으로 비동기 기록 — Load UI는 파일 있으면 표시.)</summary>
    public readonly struct CaptureThumbnailCommand
    {
        public readonly int Slot;
        public CaptureThumbnailCommand(int slot) { Slot = slot; }
    }
}
