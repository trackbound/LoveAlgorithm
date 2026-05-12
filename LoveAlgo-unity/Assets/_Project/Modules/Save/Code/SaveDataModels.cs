using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Modules.Affinity;
using LoveAlgo.Core;

namespace LoveAlgo.Story.SaveSystem
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
        public string CurrentSD;          // SD 컷씬 이름 (null이면 없음)
        public string CurrentOverlay;     // VirtualBG 오버레이 이름
        public bool IsMonologueDimShowing; // 독백 딤 표시 여부
        public bool IsFadeBlack;          // 페이드 오버레이 활성 여부
        public bool IsEyeClosed;          // 눈 감기 효과 활성 여부

        // 이벤트 발동 기록
        public List<string> FiredEvents = new();

        // 히로인 포인트 추적 데이터
        public PointTrackerSaveData PointTracker;

        // 상점/인벤토리 데이터
        public Shop.ShopSaveData ShopData;

        // 메신저 데이터
        public Phone.MessengerSaveData MessengerData;

        // 선택지 이력 (로그 복원용)
        public List<string> ChoiceHistory = new();

        // 스케줄 상태
        public bool UsedLoadingToday;
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
}
