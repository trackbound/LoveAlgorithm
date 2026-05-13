# Handoff — 씬 바인딩 작업 안내

코드 작업 완료, **씬에서 Inspector 바인딩 필요한 항목들**.

## 1. PopupManager (Popup GameObject)

**경로**: 씬 `Popup` GameObject → PopupManager 컴포넌트

**`popupPrefabs` 리스트에 추가** (공용 팝업 3개):

| 추가 항목 | 프리팹 경로 |
|-----------|------------|
| AlertPopup | `_Project/UI/Popups/Prefabs/AlertPopup.prefab` |
| ConfirmPopup | `_Project/UI/Popups/Prefabs/ConfirmPopup.prefab` |
| ToastNotification | `_Project/UI/Notifications/Prefabs/ToastNotification.prefab` |

→ Inspector에서 `popupPrefabs` size를 3으로 설정한 뒤 각 슬롯에 위 프리팹 드래그.

## 2. 모듈별 SerializeField 바인딩

각 모듈 GameObject(`_Modules/` 하위)의 신규 SerializeField에 프리팹 연결:

| Module GameObject | 필드 | 프리팹 |
|-------------------|------|--------|
| `_Modules/SaveModule` | `Save Load Popup Prefab` | `_Project/Modules/Save/Prefabs/SaveLoadPopup.prefab` |
| `_Modules/SettingsModule` | `Settings Popup Prefab` | `_Project/Modules/Settings/Prefabs/SettingsPopup.prefab` |
| `_Modules/NarrativeModule` | `Log Popup Prefab` | `_Project/Modules/Narrative/Prefabs/LogPopup.prefab` |

## 3. 검증

씬 진입 후:
- [ ] 컴파일 에러 0
- [ ] `Popup` GO 자식: Dimmer / Modal / Top (자동 생성됨)
- [ ] `_Modules/SaveModule`: 인스펙터에 SaveLoadPopup prefab 보임
- [ ] `_Modules/SettingsModule`: SettingsPopup prefab 보임
- [ ] `_Modules/NarrativeModule`: LogPopup prefab 보임
- [ ] Play 모드 진입: 세이브/로드/설정/로그 팝업 작동 확인

## 4. 옛 직렬화 참조 (확인만, 자동 해결됨)

다음 prefab들은 위치/이름 바뀌었지만 **GUID 보존**되어 씬/다른 prefab 참조는 자동 추적됨. Inspector 빨간색 표시 있으면 알려주세요:

- `BG Overlay.prefab` → `BackgroundLayer.prefab`
- `Char.prefab` → `CharacterLayer.prefab`
- `Title UI.prefab` → `TitlePanel.prefab`
- `Config.prefab` → `SettingsPopup.prefab`
- 기타 (총 50개) — 자세히는 `docs/NAMING.md` 참조

## 5. 다음 단계

씬 바인딩 완료 후:
- 씬 하이어라키 정리 (`_UI/` 하위에 Notifications/Contextual/Modals/Panels 폴더 GO 추가)
- 각 팝업 인스턴스를 Modal/Top 레이어로 자동 이동 (PopupManager가 Register 시 처리)
