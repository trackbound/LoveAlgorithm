using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;
using LoveAlgo.Story.SaveSystem;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 세이브/로드 매니저 — SaveSystem 하위 컴포넌트로 위임하는 스키니 페이새드
    /// </summary>
    public static class SaveManager
    {
        /// <summary>
        /// 자동저장 슬롯 (Continue용)
        /// </summary>
        public const int AutoSaveSlot = SaveSlotManager.AutoSaveSlot;

        /// <summary>
        /// 유저 저장 시작 슬롯
        /// </summary>
        public const int UserSlotStart = SaveSlotManager.UserSlotStart;

        /// <summary>
        /// GameManager용 간단 저장 (스크립트 위치 없이)
        /// </summary>
        public static void Save(int slot, string chapterName, GamePhase phase, int day, int actions)
        {
            Save(slot, phase, day, actions, "", "", 0, chapterName);
        }

        /// <summary>
        /// 현재 상태 저장 (전체)
        /// </summary>
        public static void Save(int slot, GamePhase phase, int day, int actions,
            string scriptName, string lineId, int lineIndex, string chapterName = null)
        {
            var data = SaveDataSerializer.CreateSaveData(phase, day, actions, scriptName, lineId, lineIndex, chapterName);
            SaveDataSerializer.SaveToFile(data, slot);
        }

        /// <summary>
        /// 저장 데이터 로드
        /// </summary>
        public static SaveData Load(int slot)
        {
            return SaveDataSerializer.LoadFromFile(slot);
        }

        /// <summary>
        /// GameState에 로드한 데이터 적용
        /// </summary>
        public static void ApplyToGameState(SaveData data)
        {
            SaveDataSerializer.ApplyToGameState(data);
        }

        /// <summary>
        /// 세이브 슬롯 존재 여부
        /// </summary>
        public static bool Exists(int slot)
        {
            return SaveSlotManager.Exists(slot);
        }

        /// <summary>
        /// 세이브 삭제
        /// </summary>
        public static void Delete(int slot)
        {
            SaveSlotManager.Delete(slot);
            SaveThumbnailManager.DeleteScreenshot(slot);
        }

        /// <summary>
        /// 모든 세이브/스크린샷 삭제 (빌드 초기화용)
        /// </summary>
        public static void DeleteAll()
        {
            SaveSlotManager.DeleteAll();
        }

        /// <summary>
        /// 모든 세이브 슬롯 정보 가져오기 (UI용) - 유저 슬롯만
        /// </summary>
        public static List<(int slot, SaveData data)> GetAllUserSaves(int maxSlots = 30)
        {
            return SaveSlotManager.GetAllUserSaves(maxSlots);
        }

        #region Screenshot

        /// <summary>
        /// 팝업 열기 전 게임 화면을 임시 파일로 미리 캡처 (비동기)
        /// </summary>
        public static async UniTask CapturePendingScreenshotAsync()
        {
            await SaveThumbnailManager.CapturePendingScreenshotAsync();
        }

        /// <summary>
        /// 동기 버전 (하위 호환 — 자동저장 등에서 사용)
        /// </summary>
        public static void CapturePendingScreenshot()
        {
            SaveThumbnailManager.CapturePendingScreenshot();
        }

        /// <summary>
        /// 임시 파일을 실제 슬롯 썸네일로 확정
        /// </summary>
        public static bool TryCommitPendingScreenshot(int slot)
        {
            return SaveThumbnailManager.TryCommitPendingScreenshot(slot);
        }

        /// <summary>
        /// 스크린샷 직접 캡처 및 저장 (자동저장 등 팝업 없는 경우)
        /// </summary>
        public static void CaptureScreenshot(int slot)
        {
            SaveThumbnailManager.CaptureScreenshot(slot);
        }

        /// <summary>
        /// 스크린샷 로드 (슬롯 UI용)
        /// </summary>
        public static Sprite LoadScreenshot(int slot)
        {
            return SaveThumbnailManager.LoadScreenshot(slot);
        }

        /// <summary>
        /// 스크린샷 삭제
        /// </summary>
        public static void DeleteScreenshot(int slot)
        {
            SaveThumbnailManager.DeleteScreenshot(slot);
        }

        #endregion
    }
}
