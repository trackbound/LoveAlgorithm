using LoveAlgo.Contracts;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Story.SaveSystem;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Save
{
    /// <summary>
    /// 세이브 모듈 진입점.
    /// SaveManager 정적 클래스를 ISave 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/SaveModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class SaveModule : MonoBehaviour, ISave
    {
        [Header("UI Prefab (모듈 응집)")]
        [SerializeField] SaveLoadPopup saveLoadPopupPrefab;

        SaveLoadPopup popupInstance;

        void Awake()
        {
            Services.Register<ISave>(this);
            if (saveLoadPopupPrefab != null && PopupSystem.Instance != null)
                popupInstance = PopupSystem.Instance.Register(saveLoadPopupPrefab);
        }

        void OnDestroy()
        {
            if (Services.TryGet<ISave>() == (ISave)this)
                Services.Unregister<ISave>();
        }

        public int AutoSaveSlot => Story.SaveManager.AutoSaveSlot;
        public int UserSlotStart => Story.SaveManager.UserSlotStart;

        public void Save(int slot, string chapterName, GamePhase phase, int day, int actions)
            => Story.SaveManager.Save(slot, chapterName, phase, day, actions);

        public void Save(int slot, GamePhase phase, int day, int actions,
            string scriptName, string lineId, int lineIndex, string chapterName = null)
            => Story.SaveManager.Save(slot, phase, day, actions, scriptName, lineId, lineIndex, chapterName);

        public SaveData Load(int slot)
            => Story.SaveManager.Load(slot);

        public void ApplyToGameState(SaveData data)
            => Story.SaveManager.ApplyToGameState(data);

        public bool Exists(int slot)
            => Story.SaveManager.Exists(slot);

        public void Delete(int slot)
            => Story.SaveManager.Delete(slot);

        public void DeleteAll()
            => Story.SaveManager.DeleteAll();

        public List<(int slot, SaveData data)> GetAllUserSaves(int maxSlots = 30)
            => Story.SaveManager.GetAllUserSaves(maxSlots);

        public UniTask CapturePendingScreenshotAsync()
            => Story.SaveManager.CapturePendingScreenshotAsync();

        public void CapturePendingScreenshot()
            => Story.SaveManager.CapturePendingScreenshot();

        public bool TryCommitPendingScreenshot(int slot)
            => Story.SaveManager.TryCommitPendingScreenshot(slot);

        public void CaptureScreenshot(int slot)
            => Story.SaveManager.CaptureScreenshot(slot);

        public Sprite LoadScreenshot(int slot)
            => Story.SaveManager.LoadScreenshot(slot);

        public void DeleteScreenshot(int slot)
            => Story.SaveManager.DeleteScreenshot(slot);

        // ── UI 진입점 ────────────────────────────────
        public async void ShowSaveUI()
        {
            await CapturePendingScreenshotAsync();
            var popup = EnsurePopup();
            popup?.ShowSave((slot, customLabel) =>
            {
                GameManager.Instance?.Save(slot, customLabel: customLabel);
                LoveAlgo.Modules.Audio.AudioManager.Instance?.PlaySaveComplete();
                PopupSystem.Instance?.Toast("저장 완료", $"슬롯 {slot}에 저장했습니다.");
            });
        }

        public void ShowLoadUI()
        {
            var popup = EnsurePopup();
            popup?.ShowLoad((slot, _) =>
            {
                GameManager.Instance?.LoadGame(slot);
                LoveAlgo.Modules.Audio.AudioManager.Instance?.PlayLoadComplete();
                PopupSystem.Instance?.Toast("로드 완료", $"슬롯 {slot}에서 불러왔습니다.");
            });
        }

        SaveLoadPopup EnsurePopup()
        {
            if (popupInstance != null) return popupInstance;
            if (saveLoadPopupPrefab == null) return null;
            var pm = PopupSystem.Instance;
            if (pm == null) return null;
            popupInstance = pm.Register(saveLoadPopupPrefab);
            return popupInstance;
        }
    }
}
