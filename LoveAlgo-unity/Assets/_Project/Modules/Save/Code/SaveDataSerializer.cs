using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using LoveAlgo.Common;
using LoveAlgo.Contracts;
using LoveAlgo.Modules.Affinity;
using LoveAlgo.Modules.Audio;
using Newtonsoft.Json;
using LoveAlgo.Core;
using LoveAlgo.Save;
using LoveAlgo.Stage;
using LoveAlgo.UI;

namespace LoveAlgo.Story.SaveSystem
{
    /// <summary>
    /// 세이브 데이터 직렬화/역직렬화 및 파일 입출력 담당
    /// </summary>
    public static class SaveDataSerializer
    {
        const string SaveFolder = "Saves";
        const string SaveExtension = ".json";
        const string TmpSuffix = ".tmp";
        const string BackupSuffix = ".bak";

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
        /// SaveData를 JSON으로 직렬화하여 파일에 저장 (atomic write + .bak 1세대 백업).
        /// 부분 실패 시 원본은 보존되며, 백업에서 복구 가능.
        /// </summary>
        public static void SaveToFile(SaveData data, int slot)
        {
            try
            {
                data.Version = SaveData.CurrentVersion;
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                AtomicWriteAllText(GetSavePath(slot), json, Encoding.UTF8);
                Debug.Log($"[SaveDataSerializer] 슬롯 {slot} 저장 완료 (v{SaveData.CurrentVersion})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataSerializer] 슬롯 {slot} 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// JSON 파일에서 SaveData 역직렬화하여 로드. 본 파일 손상 시 .bak에서 복구 시도.
        /// </summary>
        public static SaveData LoadFromFile(int slot)
        {
            string path = GetSavePath(slot);

            var data = TryLoadJson(path);
            if (data != null)
            {
                MigrateIfNeeded(data, slot);
                Debug.Log($"[SaveDataSerializer] 슬롯 {slot} 로드 완료");
                return data;
            }

            string bak = path + BackupSuffix;
            if (File.Exists(bak))
            {
                Debug.LogWarning($"[SaveDataSerializer] 슬롯 {slot} 본 파일 손상/없음 — 백업({Path.GetFileName(bak)})에서 복구 시도");
                data = TryLoadJson(bak);
                if (data != null)
                {
                    MigrateIfNeeded(data, slot);
                    try { File.Copy(bak, path, overwrite: true); }
                    catch (Exception e) { Debug.LogWarning($"[SaveDataSerializer] 백업 복원 카피 실패: {e.Message}"); }
                    return data;
                }
            }
            return null;
        }

        static SaveData TryLoadJson(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return JsonConvert.DeserializeObject<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataSerializer] 파일 로드 실패 ({Path.GetFileName(path)}): {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 옛 버전 세이브를 현재 포맷으로 변환. 필드 추가/이름 변경 시 단계별 분기 추가.
        /// </summary>
        static void MigrateIfNeeded(SaveData data, int slot)
        {
            if (data.Version == SaveData.CurrentVersion) return;

            if (data.Version > SaveData.CurrentVersion)
            {
                Debug.LogWarning(
                    $"[SaveDataSerializer] 슬롯 {slot}: 미래 버전 세이브(v{data.Version}) — 현재 게임은 v{SaveData.CurrentVersion}, 호환 안 될 수 있음");
                return;
            }

            int from = data.Version;
            // v0 (버전 도입 전) → v1: 추가 변환 없음. 새 필드는 기본값 유지.
            // (향후 v1 → v2 변환 시 여기에 단계별 분기 추가)
            data.Version = SaveData.CurrentVersion;
            Debug.Log($"[SaveDataSerializer] 슬롯 {slot}: 세이브 마이그레이션 v{from} → v{SaveData.CurrentVersion}");
        }

        /// <summary>
        /// tmp 파일에 쓴 뒤 원본을 .bak로 보존하면서 교체. 부분 실패 시 원본 손상 방지.
        /// </summary>
        internal static void AtomicWriteAllText(string path, string contents, Encoding encoding)
        {
            string tmp = path + TmpSuffix;
            File.WriteAllText(tmp, contents, encoding);
            ReplaceWithBackup(tmp, path);
        }

        internal static void AtomicWriteAllBytes(string path, byte[] bytes)
        {
            string tmp = path + TmpSuffix;
            File.WriteAllBytes(tmp, bytes);
            ReplaceWithBackup(tmp, path);
        }

        static void ReplaceWithBackup(string tmp, string target)
        {
            string bak = target + BackupSuffix;
            if (File.Exists(target))
            {
                // File.Replace: 원본 → bak, tmp → 원본 위치. 같은 볼륨 내에서 원자적.
                File.Replace(tmp, target, bak);
            }
            else
            {
                File.Move(tmp, target);
            }
        }

        /// <summary>
        /// 현재 장면 상태를 SaveData에 캡처 (배경, 캐릭터, BGM, CG, 오버레이, 딤, FX)
        /// </summary>
        public static void CaptureStageState(SaveData data)
        {
            var stage = StageModule.Instance;

            // 배경
            data.CurrentBG = stage?.Background?.CurrentBackground ?? "";

            // BGM — C3-6: AudioManager 직접 참조 대신 BGMChangedEvent 구독 캐시
            data.CurrentBGM = LastBGMTracker.CurrentBGM;

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

            // 스케줄 상태 복원
            var scheduleUI = Services.TryGet<ISchedule>()?.ScheduleUI;
            if (scheduleUI != null)
                scheduleUI.UsedLoadingToday = data.UsedLoadingToday;
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

            // 스케줄 상태 저장
            var scheduleUI = Services.TryGet<ISchedule>()?.ScheduleUI;
            if (scheduleUI != null)
                data.UsedLoadingToday = scheduleUI.UsedLoadingToday;

            // 장면 상태 저장 (배경, 캐릭터, BGM)
            CaptureStageState(data);

            return data;
        }
    }
}
