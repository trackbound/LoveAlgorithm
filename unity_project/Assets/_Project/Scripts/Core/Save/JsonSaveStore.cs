using System;
using System.IO;
using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 세이브 파일 I/O (JSON, persistentDataPath). 슬롯 = 정수(0=자동저장, 1+=유저).
    /// 직렬화 라이브러리: Unity JsonUtility(외부 의존 0). Dictionary 미지원이라 데이터 모델은
    /// 엔트리 리스트로 설계됨(GameStateData). 정책 변경 시 ADR 기록.
    /// </summary>
    public static class JsonSaveStore
    {
        public const int AutoSaveSlot = 0;

        static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        static string SlotPath(int slot) => Path.Combine(Dir, $"save_{slot}.json");
        public static string ThumbnailPath(string fileName) => Path.Combine(Dir, fileName);
        public static string ThumbnailFileFor(int slot) => $"thumb_{slot}.png";

        public static bool Exists(int slot) => File.Exists(SlotPath(slot));

        /// <summary>슬롯에 저장. 실패 시 false + 에러 로그.</summary>
        public static bool Save(int slot, SaveData data)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                data.version = SaveData.CurrentVersion;
                File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, prettyPrint: true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonSaveStore] 슬롯 {slot} 저장 실패: {e}");
                return false;
            }
        }

        /// <summary>슬롯에서 로드. 없거나 손상 시 null(호출부에서 새 게임 처리).</summary>
        public static SaveData Load(int slot)
        {
            var path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonSaveStore] 슬롯 {slot} 로드 실패(손상?): {e}");
                return null;
            }
        }

        public static void Delete(int slot)
        {
            try
            {
                if (File.Exists(SlotPath(slot))) File.Delete(SlotPath(slot));
            }
            catch (Exception e) { Debug.LogError($"[JsonSaveStore] 슬롯 {slot} 삭제 실패: {e}"); }
        }
    }
}
