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

    /// <summary>저장 시 썸네일 캡처 요청. 인게임 ThumbnailCaptureController가 구독해 슬롯 PNG를 기록.
    /// <see cref="UseCache"/>=true(수동 저장)면 라이브 캡처 대신 <see cref="PrimeThumbnailCacheCommand"/>로
    /// 미리 떠둔 캐시 PNG를 그대로 기록 → 슬롯 클릭마다 UI를 끄는 깜빡임 제거. false(자동저장 등)면 라이브 캡처.</summary>
    public readonly struct CaptureThumbnailCommand
    {
        public readonly int Slot;
        public readonly bool UseCache;
        public CaptureThumbnailCommand(int slot, bool useCache = false) { Slot = slot; UseCache = useCache; }
    }

    /// <summary>썸네일 캐시 예열 요청(세이브 팝업이 열리기 직전 1회). ThumbnailCaptureController가 스테이지-only
    /// 화면을 캡처해 메모리에 캐싱하고 <see cref="Handle"/>을 완료한다. 팝업은 완료를 기다렸다 표시 →
    /// 캡처 중엔 팝업이 아직 안 보여 깜빡임이 팝업 등장에 묻힌다. 이후 수동 저장 슬롯 클릭은 캐시를 재사용(무깜빡임).
    /// 구독자(인게임 컨트롤러)가 없으면 핸들이 안 풀리므로, 호출부는 짧은 프레임 가드 후 그냥 표시한다.</summary>
    public readonly struct PrimeThumbnailCacheCommand
    {
        public readonly CompletionHandle Handle;
        public PrimeThumbnailCacheCommand(CompletionHandle handle) { Handle = handle; }
    }

    /// <summary>슬롯 썸네일 PNG 기록 완료 통지(ThumbnailCaptureController 발행). 캡처가 프레임 종료 비동기라
    /// 저장 직후 갱신으론 썸네일이 안 잡힘 — 세이브 팝업이 이걸 받아 해당 페이지를 재갱신한다.</summary>
    public readonly struct ThumbnailSavedEvent
    {
        public readonly int Slot;
        public ThumbnailSavedEvent(int slot) { Slot = slot; }
    }
}
