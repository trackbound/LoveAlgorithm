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

        // TODO: 장면 상태 (나중에)
        // public string CurrentBG;
        // public List<CharacterState> Characters;
        // public string CurrentBGM;
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
    }
}
