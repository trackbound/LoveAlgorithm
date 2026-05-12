using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;
using LoveAlgo.Story.SaveSystem;
using UnityEngine;

namespace LoveAlgo.Save
{
    /// <summary>
    /// 세이브/로드 모듈 외부 계약.
    /// 구현: <see cref="SaveModule"/>.
    /// </summary>
    public interface ISave
    {
        int AutoSaveSlot { get; }
        int UserSlotStart { get; }

        /// <summary>간단 저장 (스크립트 위치 없이).</summary>
        void Save(int slot, string chapterName, GamePhase phase, int day, int actions);

        /// <summary>전체 상태 저장.</summary>
        void Save(int slot, GamePhase phase, int day, int actions,
            string scriptName, string lineId, int lineIndex, string chapterName = null);

        /// <summary>슬롯 로드.</summary>
        SaveData Load(int slot);

        /// <summary>로드한 데이터를 GameState에 적용.</summary>
        void ApplyToGameState(SaveData data);

        /// <summary>슬롯 존재 여부.</summary>
        bool Exists(int slot);

        /// <summary>슬롯 삭제 (스크린샷 포함).</summary>
        void Delete(int slot);

        /// <summary>전체 삭제.</summary>
        void DeleteAll();

        /// <summary>유저 슬롯 목록 (UI용).</summary>
        List<(int slot, SaveData data)> GetAllUserSaves(int maxSlots = 30);

        // ── 스크린샷 ──────────────────────────────────────────────

        UniTask CapturePendingScreenshotAsync();
        void CapturePendingScreenshot();
        bool TryCommitPendingScreenshot(int slot);
        void CaptureScreenshot(int slot);
        Sprite LoadScreenshot(int slot);
        void DeleteScreenshot(int slot);
    }
}
