import os

base = r"C:\Users\podola\repos\LoveAlgorithm\LoveAlgorithm\.kilo\worktrees\heartbreaking-anteater\LoveAlgo-unity\Assets\Scripts\Story\SaveSystem"

# SaveDataSerializer.cs
with open(os.path.join(base, "SaveDataSerializer.cs"), "w", encoding="utf-8") as f:
    f.write(r"""using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using LoveAlgo.Core;

namespace LoveAlgo.Story.SaveSystem
{
    /// <summary>
    /// 세이브 데이터 직렬화/역직렬화 및 파일 입출력 담당
    /// </summary>
    public static class SaveDataSerializer
    {
        const string SaveFolder = "Saves";
        const string SaveExtension = ".json";

        /// <summary>
        /// 세이브 파일 경로 반환
        /// </summary>
        public static string GetSavePath(int slot)
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"save_{slot:D2}{SaveExtension}");
        }

        /// <summary>
        /// SaveData를 JSON으로 직렬화하여 파일에 저장
        /// </summary>
        public static void SaveToFile(SaveData data, int slot)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetSavePath(slot), json, System.Text.Encoding.UTF8);
                Debug.Log($"[SaveDataSerializer] 슬롯 {slot} 저장 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataSerializer] 슬롯 {slot} 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// JSON 파일에서 SaveData 역직렬화하여 로드
        /// </summary>
        public static SaveData LoadFromFile(int slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var data = JsonConvert.DeserializeObject<SaveData>(json);
                Debug.Log($"[SaveDataSerializer] 슬롯 {slot} 로드 완료");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataSerializer] 슬롯 {slot} 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 현재 장면 상태를 SaveData에 캡처 (배경, 캐릭터, BGM, CG, 오버레이, 딤, FX)
        /// </summary>
        public static void CaptureStageState(SaveData data)
        {
            var stage = StageManager.Instance;

            // 배경
            data.CurrentBG = stage?.Background?.CurrentBackground ?? "";

            // BGM
            data.CurrentBGM = AudioManager.Instance?.CurrentBGM ?? "";

            // 캐릭터 슬롯
            data.Characters.Clear();
            var charLayer = stage?.Character;
            if (charLayer != null)
            {
                foreach (var slotName in new[] { "L", "C", "R" })
                {
                    SlotPosition pos;
                    switch (slotName)
                    {
                        case "L": pos = SlotPosition.L; break;
                        case "R": pos = SlotPosition.R; break;
                        default:  pos = SlotPosition.C; break;
                    }
                    var slot = charLayer.GetSlot(pos);
                    if (slot != null && !slot.IsEmpty)
                    {
                        data.Characters.Add(new CharacterSaveInfo
                        {
                            Slot = slotName,
                            Character = slot.CurrentCharacter,
                            Emote = slot.CurrentEmote ?? "Default"
                        });
                    }
                }
            }

            // CG 레이어
            var cg = stage?.CG;
            data.CurrentCG = (cg != null && cg.IsShowing) ? cg.CurrentCG : null;

            // SD 컷씬 레이어
            var sd = stage?.SDCutscene;
            data.CurrentSD = (sd != null && sd.IsShowing) ? sd.CurrentSD : null;

            // VirtualBG 오버레이
            var overlay = stage?.VirtualBG;
            data.CurrentOverlay = (overlay != null && overlay.IsShowing) ? overlay.CurrentOverlay : null;

            // 독백 딤
            data.IsMonologueDimShowing = stage?.MonologueDim?.IsShowing ?? false;

            // 화면 효과 상태
            var fx = ScreenFX.Instance;
            data.IsFadeBlack = fx?.IsFadeBlack ?? false;
            data.IsEyeClosed = fx?.IsEyeClosed ?? false;
        }

        /// <summary>
        /// 로드한 SaveData를 GameState에 적용
        /// </summary>
        public static void ApplyToGameState(SaveData data)
        {
            if (data == null || GameState.Instance == null) return;

            GameState.Instance.SetPlayerName(data.PlayerName);
            GameState.Instance.SetMoney(data.Money);
            GameState.Instance.SetAllLovePoints(data.LovePoints);
            GameState.Instance.SetAllFlags(data.Flags);
            GameState.Instance.SetStat("Str", data.Strength);
            GameState.Instance.SetStat("Int", data.Intelligence);
            GameState.Instance.SetStat("Soc", data.Sociability);
            GameState.Instance.SetStat("Per", data.Perseverance);
            GameState.Instance.SetStat("Fatigue", data.Fatigue);

            // 이벤트 발동 기록 복원
            DayEventTable.RestoreFiredEvents(data.FiredEvents);

            // 히로인 포인트 추적 데이터 복원
            if (data.PointTracker != null)
                HeroinePointTracker.RestoreFromSave(data.PointTracker);
            else
                HeroinePointTracker.Reset();

            // 상점/인벤토리 데이터 복원
            if (data.ShopData != null)
                Shop.ShopManager.RestoreFromSave(data.ShopData);
            else
                Shop.ShopManager.Reset();

            // 메신저 데이터 복원
            if (data.MessengerData != null)
                Phone.MessengerManager.RestoreFromSave(data.MessengerData);
            else
                Phone.MessengerManager.Reset();

            // 선택지 이력 복원
            GameState.Instance.SetChoiceHistory(data.ChoiceHistory);
        }

        /// <summary>
        /// 현재 GameState에서 SaveData 생성
        /// </summary>
        public static SaveData CreateSaveData(GamePhase phase, int day, int actions,
            string scriptName, string lineId, int lineIndex, string chapterName = null)
        {
            var data = new SaveData
            {
                Phase = phase,
                CurrentDay = day,
                RemainingActions = actions,
                ScriptName = scriptName,
                LineId = lineId,
                LineIndex = lineIndex,
                SaveTime = DateTime.Now,
                ChapterName = chapterName
            };

            // GameState에서 데이터 복사
            if (GameState.Instance != null)
            {
                data.PlayerName = GameState.Instance.PlayerName;
                data.Money = GameState.Instance.Money;
                data.LovePoints = GameState.Instance.GetAllLovePoints();
                data.Flags = GameState.Instance.GetAllFlags();
                data.Strength = GameState.Instance.GetStat("Str");
                data.Intelligence = GameState.Instance.GetStat("Int");
                data.Sociability = GameState.Instance.GetStat("Soc");
                data.Perseverance = GameState.Instance.GetStat("Per");
                data.Fatigue = GameState.Instance.GetStat("Fatigue");
            }

            // 이벤트 발동 기록 저장
            data.FiredEvents = new List<string>(DayEventTable.GetFiredEvents());

            // 히로인 포인트 추적 데이터 저장
            data.PointTracker = HeroinePointTracker.GetSaveData();

            // 상점/인벤토리 데이터 저장
            data.ShopData = Shop.ShopManager.GetSaveData();

            // 메신저 데이터 저장
            data.MessengerData = Phone.MessengerManager.GetSaveData();

            // 선택지 이력 저장
            if (GameState.Instance != null)
                data.ChoiceHistory = GameState.Instance.GetChoiceHistory();

            // 장면 상태 저장 (배경, 캐릭터, BGM)
            CaptureStageState(data);

            return data;
        }
    }
}
""")

# SaveSlotManager.cs
with open(os.path.join(base, "SaveSlotManager.cs"), "w", encoding="utf-8") as f:
    f.write(r"""using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace LoveAlgo.Story.SaveSystem
{
    /// <summary>
    /// 세이브 슬롯 파일 관리 (존재 여부, 삭제, 목록 조회)
    /// </summary>
    public static class SaveSlotManager
    {
        const string SaveFolder = "Saves";
        const string SaveExtension = ".json";

        /// <summary>
        /// 자동저장 슬롯 (Continue용)
        /// </summary>
        public const int AutoSaveSlot = 0;

        /// <summary>
        /// 유저 저장 시작 슬롯
        /// </summary>
        public const int UserSlotStart = 1;

        /// <summary>
        /// 세이브 파일 경로 반환
        /// </summary>
        public static string GetSavePath(int slot)
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"save_{slot:D2}{SaveExtension}");
        }

        /// <summary>
        /// 세이브 슬롯 존재 여부 확인
        /// </summary>
        public static bool Exists(int slot)
        {
            return File.Exists(GetSavePath(slot));
        }

        /// <summary>
        /// 세이브 데이터 삭제 (스크린샷 제외 - 호출자가 처리)
        /// </summary>
        public static void Delete(int slot)
        {
            string path = GetSavePath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveSlotManager] 슬롯 {slot} 삭제");
            }
        }

        /// <summary>
        /// 모든 세이브 데이터 삭제 (빌드 초기화용)
        /// </summary>
        public static void DeleteAll()
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
                Debug.Log("[SaveSlotManager] 모든 세이브 데이터 삭제");
            }
        }

        /// <summary>
        /// 모든 유저 세이브 슬롯 정보 가져오기 (UI용)
        /// </summary>
        public static List<(int slot, SaveData data)> GetAllUserSaves(int maxSlots = 30)
        {
            var saves = new List<(int, SaveData)>();
            for (int i = UserSlotStart; i < maxSlots; i++)
            {
                if (Exists(i))
                {
                    saves.Add((i, SaveDataSerializer.LoadFromFile(i)));
                }
            }
            return saves;
        }
    }
}
""")

# SaveThumbnailManager.cs
with open(os.path.join(base, "SaveThumbnailManager.cs"), "w", encoding="utf-8") as f:
    f.write(r"""using System;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LoveAlgo.UI;

namespace LoveAlgo.Story.SaveSystem
{
    /// <summary>
    /// 썸네일 스크린샷 캡처, 생성, UI 숨김/복원 담당
    /// </summary>
    public static class SaveThumbnailManager
    {
        const string SaveFolder = "Saves";
        const int ThumbnailWidth = 400;
        const int ThumbnailHeight = 128;

        struct ThumbnailUISnapshot
        {
            public bool DialogueActive;
            public bool ChoiceActive;
            public bool ScheduleActive;
            public bool TitleActive;
            public bool UsernameActive;
            public bool PlaceActive;
            public PopupManager.ThumbnailPopupState PopupState;
            public bool HasPopupState;
        }

        /// <summary>
        /// 스크린샷 저장 경로
        /// </summary>
        public static string GetScreenshotPath(int slot)
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"save_{slot:D2}_thumb.png");
        }

        /// <summary>
        /// 팝업 열기 전 게임 화면을 임시 파일로 미리 캡처 (비동기)
        /// UI를 숨기고 1프레임 대기 후 캡처 - UI/팝업이 썸네일에 포함되지 않음
        /// </summary>
        public static async UniTask CapturePendingScreenshotAsync()
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var tex = await CaptureStageOnlyTextureAsync();
                var thumb = CropAndScaleTexture(tex, ThumbnailWidth, ThumbnailHeight);
                File.WriteAllBytes(
                    Path.Combine(folder, "save_pending_thumb.png"),
                    thumb.EncodeToPNG()
                );
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(thumb);
                Debug.Log("[SaveThumbnailManager] 팬딩 스크린샷 캡처 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 팬딩 스크린샷 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 동기 버전 (하위 호환 - 자동저장 등에서 사용)
        /// 같은 프레임 캡처이므로 UI가 찍힐 수 있음. 가능하면 Async 버전 사용
        /// </summary>
        public static void CapturePendingScreenshot()
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var tex = CaptureStageOnlyTextureSync();
                var thumb = CropAndScaleTexture(tex, ThumbnailWidth, ThumbnailHeight);
                File.WriteAllBytes(
                    Path.Combine(folder, "save_pending_thumb.png"),
                    thumb.EncodeToPNG()
                );
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(thumb);
                Debug.Log("[SaveThumbnailManager] 팬딩 스크린샷 캡처 완료 (sync)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 팬딩 스크린샷 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 임시 파일을 실제 슬롯 썸네일로 확정
        /// 성공 시 true, 없으면 false
        /// </summary>
        public static bool TryCommitPendingScreenshot(int slot)
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
                string pending = Path.Combine(folder, "save_pending_thumb.png");
                if (File.Exists(pending))
                {
                    File.Copy(pending, GetScreenshotPath(slot), overwrite: true);
                    File.Delete(pending);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 썸네일 확정 실패: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// 스크린샷 직접 캡처 및 저장 (자동저장 등 팝업 없는 경우)
        /// </summary>
        public static void CaptureScreenshot(int slot)
        {
            try
            {
                var tex = CaptureStageOnlyTextureSync();
                var thumb = CropAndScaleTexture(tex, ThumbnailWidth, ThumbnailHeight);
                byte[] png = thumb.EncodeToPNG();
                File.WriteAllBytes(GetScreenshotPath(slot), png);

                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(thumb);
                Debug.Log($"[SaveThumbnailManager] 슬롯 {slot} 스크린샷 저장");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 스크린샷 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 스크린샷 로드 (슬롯 UI용)
        /// </summary>
        public static Sprite LoadScreenshot(int slot)
        {
            string path = GetScreenshotPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                byte[] png = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(png);
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 스크린샷 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 스크린샷 삭제
        /// </summary>
        public static void DeleteScreenshot(int slot)
        {
            string path = GetScreenshotPath(slot);
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// 비동기 캡처: UI 숨김 - 1프레임 대기(렌더 반영) - 캡처 - UI 복원
        /// </summary>
        static async UniTask<Texture2D> CaptureStageOnlyTextureAsync()
        {
            var snapshot = HideUIForThumbnailCapture();
            try
            {
                await UniTask.WaitForEndOfFrame();
                return ScreenCapture.CaptureScreenshotAsTexture();
            }
            finally
            {
                RestoreUIAfterThumbnailCapture(snapshot);
            }
        }

        /// <summary>
        /// 동기 캡처 (폴백) - ForceUpdateCanvases 후 즉시 캡처
        /// </summary>
        static Texture2D CaptureStageOnlyTextureSync()
        {
            var snapshot = HideUIForThumbnailCapture();
            try
            {
                Canvas.ForceUpdateCanvases();
                return ScreenCapture.CaptureScreenshotAsTexture();
            }
            finally
            {
                RestoreUIAfterThumbnailCapture(snapshot);
            }
        }

        /// <summary>
        /// 썸네일 캡처를 위해 UI 요소들 숨김
        /// </summary>
        static ThumbnailUISnapshot HideUIForThumbnailCapture()
        {
            var ui = UIManager.Instance;
            var snapshot = new ThumbnailUISnapshot();

            if (ui != null)
            {
                if (ui.DialogueUI != null)
                {
                    snapshot.DialogueActive = ui.DialogueUI.gameObject.activeSelf;
                    ui.DialogueUI.gameObject.SetActive(false);
                }
                if (ui.ChoiceUI != null)
                {
                    snapshot.ChoiceActive = ui.ChoiceUI.gameObject.activeSelf;
                    ui.ChoiceUI.gameObject.SetActive(false);
                }
                if (ui.ScheduleUI != null)
                {
                    snapshot.ScheduleActive = ui.ScheduleUI.gameObject.activeSelf;
                    ui.ScheduleUI.gameObject.SetActive(false);
                }
                if (ui.TitleUI != null)
                {
                    snapshot.TitleActive = ui.TitleUI.gameObject.activeSelf;
                    ui.TitleUI.gameObject.SetActive(false);
                }
                if (ui.UsernameUI != null)
                {
                    snapshot.UsernameActive = ui.UsernameUI.gameObject.activeSelf;
                    ui.UsernameUI.gameObject.SetActive(false);
                }
                if (ui.PlaceUI != null)
                {
                    snapshot.PlaceActive = ui.PlaceUI.gameObject.activeSelf;
                    ui.PlaceUI.gameObject.SetActive(false);
                }
            }

            var popup = PopupManager.Instance;
            if (popup != null)
            {
                snapshot.PopupState = popup.HideForThumbnailCapture();
                snapshot.HasPopupState = true;
            }

            return snapshot;
        }

        /// <summary>
        /// 썸네일 캡처 후 숨겼던 UI 요소들 복원
        /// </summary>
        static void RestoreUIAfterThumbnailCapture(ThumbnailUISnapshot snapshot)
        {
            var ui = UIManager.Instance;
            if (ui != null)
            {
                if (ui.DialogueUI != null) ui.DialogueUI.gameObject.SetActive(snapshot.DialogueActive);
                if (ui.ChoiceUI != null) ui.ChoiceUI.gameObject.SetActive(snapshot.ChoiceActive);
                if (ui.ScheduleUI != null) ui.ScheduleUI.gameObject.SetActive(snapshot.ScheduleActive);
                if (ui.TitleUI != null) ui.TitleUI.gameObject.SetActive(snapshot.TitleActive);
                if (ui.UsernameUI != null) ui.UsernameUI.gameObject.SetActive(snapshot.UsernameActive);
                if (ui.PlaceUI != null) ui.PlaceUI.gameObject.SetActive(snapshot.PlaceActive);
            }

            var popup = PopupManager.Instance;
            if (popup != null && snapshot.HasPopupState)
            {
                popup.RestoreAfterThumbnailCapture(snapshot.PopupState);
            }
        }

        /// <summary>
        /// 텍스쳐 중앙 크롭 및 축소
        /// </summary>
        static Texture2D CropAndScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var contentRect = DetectContentRect(source);

            float sourceAspect = (float)contentRect.width / contentRect.height;
            float targetAspect = (float)targetWidth / targetHeight;

            int cropWidth = contentRect.width;
            int cropHeight = contentRect.height;
            int cropX = contentRect.x;
            int cropY = contentRect.y;

            if (sourceAspect > targetAspect)
            {
                cropWidth = Mathf.RoundToInt(contentRect.height * targetAspect);
                cropX = contentRect.x + (contentRect.width - cropWidth) / 2;
            }
            else if (sourceAspect < targetAspect)
            {
                cropHeight = Mathf.RoundToInt(contentRect.width / targetAspect);
                cropY = contentRect.y + (contentRect.height - cropHeight) / 2;
            }

            var cropped = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false);
            cropped.SetPixels(source.GetPixels(cropX, cropY, cropWidth, cropHeight));
            cropped.Apply();

            var scaled = ScaleTexture(cropped, targetWidth, targetHeight);
            UnityEngine.Object.Destroy(cropped);
            return scaled;
        }

        /// <summary>
        /// 캡처 원본에서 레터박스/필러박스(검정 여백) 영역 탐지
        /// </summary>
        static RectInt DetectContentRect(Texture2D source)
        {
            int w = source.width;
            int h = source.height;
            if (w <= 0 || h <= 0)
                return new RectInt(0, 0, w, h);

            var pixels = source.GetPixels32();
            const int threshold = 10;
            const int step = 3;

            bool ColumnHasContent(int x)
            {
                for (int y = 0; y < h; y += step)
                {
                    var c = pixels[(y * w) + x];
                    if (c.a > threshold && (c.r > threshold || c.g > threshold || c.b > threshold))
                        return true;
                }
                return false;
            }

            bool RowHasContent(int y)
            {
                int baseIdx = y * w;
                for (int x = 0; x < w; x += step)
                {
                    var c = pixels[baseIdx + x];
                    if (c.a > threshold && (c.r > threshold || c.g > threshold || c.b > threshold))
                        return true;
                }
                return false;
            }

            int left = 0;
            while (left < w - 1 && !ColumnHasContent(left)) left++;

            int right = w - 1;
            while (right > left && !ColumnHasContent(right)) right--;

            int bottom = 0;
            while (bottom < h - 1 && !RowHasContent(bottom)) bottom++;

            int top = h - 1;
            while (top > bottom && !RowHasContent(top)) top--;

            int contentWidth = Mathf.Max(1, right - left + 1);
            int contentHeight = Mathf.Max(1, top - bottom + 1);
            return new RectInt(left, bottom, contentWidth, contentHeight);
        }

        /// <summary>
        /// 텍스처 리사이즈 (RenderTexture 사용)
        /// </summary>
        static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                result.Apply();
                return result;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
""")

print("All 3 files written successfully")
