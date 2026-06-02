using System;
using LoveAlgo.Core; // GameStateSO, GameStateData, SaveData, JsonSaveStore

namespace LoveAlgo.Save
{
    /// <summary>
    /// 세이브 매핑 순수층: GameStateSO ↔ <see cref="SaveData"/> ↔ <see cref="JsonSaveStore"/>.
    /// MonoBehaviour/EventBus를 모른다 = 결정적이고 EditMode에서 테스트 가능(ADR-007).
    /// 스키마는 Core <see cref="SaveData"/>, I/O는 Core <see cref="JsonSaveStore"/>에 위임 — 이 층은 변환만.
    /// 썸네일 캡처/슬롯 메타 확장은 해당 시스템(M5 UI) 재작성 시.
    /// </summary>
    public static class SaveService
    {
        /// <summary>현재 런타임 상태를 <see cref="SaveData"/>로 스냅샷. savedAtUtc=호출 시점(ISO8601), chapterLabel=표시용.</summary>
        public static SaveData Capture(GameStateSO gs, string chapterLabel = "")
        {
            return new SaveData
            {
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                chapterLabel = chapterLabel ?? "",
                state = gs != null ? gs.Data : new GameStateData()
            };
        }

        /// <summary>슬롯에 저장(capture + JsonSaveStore.Save). gs null이면 false.</summary>
        public static bool Save(int slot, GameStateSO gs, string chapterLabel = "")
        {
            if (gs == null) return false;
            return JsonSaveStore.Save(slot, Capture(gs, chapterLabel));
        }

        /// <summary>슬롯에서 로드해 gs에 복원. 파일 없음/손상/gs null이면 false(상태 불변).</summary>
        public static bool Load(int slot, GameStateSO gs)
        {
            if (gs == null) return false;
            var data = JsonSaveStore.Load(slot);
            if (data == null) return false;
            gs.Load(data.state);
            return true;
        }
    }
}
