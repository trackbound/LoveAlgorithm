# 세이브 데이터 스키마

## 저장 위치

`Application.persistentDataPath/Saves/save_{slot}.json`
- `save_00.json` — 자동 저장
- `save_01~29.json` — 수동 저장 슬롯

## SaveData 구조

```
SaveData
├── Phase: GamePhase           # 현재 Phase
├── Day: int                   # 현재 일차
├── RemainingActions: int      # 남은 행동 수
├── PlayerName: string         # 플레이어 이름
│
├── Stats                      # 플레이어 스탯
│   ├── Str, Int, Soc, Per     # 4대 스탯
│   └── Fatigue                # 피로도
│
├── LovePoints                 # 히로인별 호감도
│   └── Dict<string, int>      # "Roa": 45 등
│
├── Flags                      # 플래그
│   └── Dict<string, bool>     # "Met_Roa": true 등
│
├── Money: int                 # 소지금
│
├── PointTrackerSaveData       # HeroinePointTracker 데이터
│   └── 히로인별 이벤트/선물/미니게임 포인트
│
├── ShopSaveData               # 인벤토리
│   └── Dict<string, int>      # 아이템별 보유 수량
│
├── MessengerSaveData          # 메신저 채팅 기록
│   └── 히로인별 ChatMessage 리스트
│
└── FiredEvents                # 발동된 이벤트 목록
    └── List<string>           # 스크립트 이름 리스트
```

## 직렬화

- Newtonsoft.Json 사용
- `SaveManager.Save(slot, ...)` → JSON 직렬화 → 파일 쓰기
- `SaveManager.Load(slot)` → 파일 읽기 → JSON 역직렬화 → `ApplyToGameState()`

> ⚠️ 세이브 포맷 변경 시 기존 세이브 파일과 호환 불가. 마이그레이션 계획 수립 후 변경할 것.
