using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using LoveAlgo.Contracts;

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
        /// 세이브 데이터 삭제 (본 파일 + .bak 백업, 스크린샷 제외 — 호출자가 처리)
        /// </summary>
        public static void Delete(int slot)
        {
            string path = GetSavePath(slot);
            bool deleted = false;
            if (File.Exists(path))
            {
                File.Delete(path);
                deleted = true;
            }
            string bak = path + ".bak";
            if (File.Exists(bak))
            {
                File.Delete(bak);
                deleted = true;
            }
            if (deleted)
                Debug.Log($"[SaveSlotManager] 슬롯 {slot} 삭제");
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
