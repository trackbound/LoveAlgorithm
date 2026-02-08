using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using LoveAlgo.Core;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 세이브 데이터 구조
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // 게임 진행 상태
        public GamePhase Phase;
        public int CurrentDay;
        public int RemainingActions;

        // 스크립트 위치
        public string ScriptName;       // CSV 파일명
        public string LineId;           // 현재 LineID (없으면 null)
        public int LineIndex;           // 현재 인덱스

        // GameState
        public string PlayerName;
        public int Money;
        public Dictionary<string, int> LovePoints = new();
        public Dictionary<string, bool> Flags = new();
        public int Strength;
        public int Intelligence;
        public int Sociability;
        public int Perseverance;
        public int Fatigue;

        // 메타
        public DateTime SaveTime;
        public string ChapterName;      // 표시용 (선택)

        // 장면 상태 (미술 복원용)
        public string CurrentBG;
        public string CurrentBGM;
        public List<CharacterSaveInfo> Characters = new();

        // 추가 레이어 상태
        public string CurrentCG;          // CG 이름 (null이면 없음)
        public string CurrentOverlay;     // VirtualBG 오버레이 이름
        public bool IsMonologueDimShowing; // 독백 딤 표시 여부
        public bool IsFadeBlack;          // 페이드 오버레이 활성 여부
        public bool IsEyeClosed;          // 눈 감기 효과 활성 여부
    }

    /// <summary>
    /// 캐릭터 슬롯 저장 정보
    /// </summary>
    [Serializable]
    public class CharacterSaveInfo
    {
        public string Slot;       // "L", "C", "R"
        public string Character;  // 캐릭터 ID
        public string Emote;      // 표정
    }

    /// <summary>
    /// 세이브/로드 매니저
    /// </summary>
    public static class SaveManager
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

        static string GetSavePath(int slot)
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"save_{slot:D2}{SaveExtension}");
        }

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

            // 장면 상태 저장 (배경, 캐릭터, BGM)
            CaptureStageState(data);

            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetSavePath(slot), json, System.Text.Encoding.UTF8);
                Debug.Log($"[SaveManager] 슬롯 {slot} 저장 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 슬롯 {slot} 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 저장 데이터 로드
        /// </summary>
        public static SaveData Load(int slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] 슬롯 {slot} 세이브 없음");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var data = JsonConvert.DeserializeObject<SaveData>(json);
                Debug.Log($"[SaveManager] 슬롯 {slot} 로드 완료");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 슬롯 {slot} 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// GameState에 로드한 데이터 적용
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
        }

        /// <summary>
        /// 세이브 슬롯 존재 여부
        /// </summary>
        public static bool Exists(int slot)
        {
            return File.Exists(GetSavePath(slot));
        }

        /// <summary>
        /// 세이브 삭제
        /// </summary>
        public static void Delete(int slot)
        {
            string path = GetSavePath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveManager] 슬롯 {slot} 삭제");
            }
        }

        /// <summary>
        /// 장면 상태 캡처 (배경, 캐릭터, BGM, CG, 오버레이, 딤, FX)
        /// </summary>
        static void CaptureStageState(SaveData data)
        {
            var stage = Core.StageManager.Instance;

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

            // VirtualBG 오버레이
            var overlay = stage?.VirtualBG;
            data.CurrentOverlay = (overlay != null && overlay.IsShowing) ? overlay.CurrentOverlay : null;

            // 독백 딤
            data.IsMonologueDimShowing = stage?.MonologueDim?.IsShowing ?? false;

            // 화면 효과 상태
            var fx = Core.ScreenFX.Instance;
            data.IsFadeBlack = fx?.IsFadeBlack ?? false;
            data.IsEyeClosed = fx?.IsEyeClosed ?? false;
        }

        /// <summary>
        /// 모든 세이브 슬롯 정보 가져오기 (UI용) - 유저 슬롯만
        /// </summary>
        public static List<(int slot, SaveData data)> GetAllUserSaves(int maxSlots = 30)
        {
            var saves = new List<(int, SaveData)>();
            for (int i = UserSlotStart; i < maxSlots; i++)
            {
                if (Exists(i))
                {
                    saves.Add((i, Load(i)));
                }
            }
            return saves;
        }

        #region Screenshot

        /// <summary>
        /// 스크린샷 저장 경로
        /// </summary>
        static string GetScreenshotPath(int slot)
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"save_{slot:D2}_thumb.png");
        }

        /// <summary>
        /// 스크린샷 캐퍼 및 저장
        /// </summary>
        public static void CaptureScreenshot(int slot)
        {
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                // 썬네일 크기로 축소 (256x144)
                var thumb = ScaleTexture(tex, 256, 144);
                byte[] png = thumb.EncodeToPNG();
                File.WriteAllBytes(GetScreenshotPath(slot), png);
                
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(thumb);
                Debug.Log($"[SaveManager] 슬롯 {slot} 스크린샷 저장");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] 스크린샷 저장 실패: {e.Message}");
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
                Debug.LogWarning($"[SaveManager] 스크린샷 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 텍스쳐 축소
        /// </summary>
        static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
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

        #endregion
    }
}
