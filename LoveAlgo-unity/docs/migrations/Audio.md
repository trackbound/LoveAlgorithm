# Audio Migration Recipe

## 출처 (Source)
- `Assets/Scripts/Story/AudioManager.cs` (Singleton MonoBehaviour, ~720 lines)
- `Assets/Scripts/Story/AudioSettings.cs` (ScriptableObject)
- `Assets/Scripts/Modules/Audio/AudioModule.cs` (현재 래퍼)
- `Assets/Scripts/Modules/Audio/IAudio.cs`
- `Assets/Scripts/Modules/Audio/Events/BGMChangedEvent.cs`

## 목적지 (Destination)
```
Assets/_Project/Modules/Audio/Code/
├── AudioManager.cs       (이전: Story/AudioManager.cs)
├── AudioSettings.cs      (이전: Story/AudioSettings.cs)
├── AudioModule.cs        (이전: Modules/Audio/AudioModule.cs)
├── IAudio.cs             (이전: Modules/Audio/IAudio.cs)
└── Events/
    └── BGMChangedEvent.cs
```

`Assets/Resources/Audio/*` 및 `Assets/Resources/Data/AudioSettings.asset` 는 **이동하지 않음** (Resources.Load 경로 + GUID 참조 보존).

## 네임스페이스
| 파일 | old | new |
|------|-----|-----|
| AudioManager.cs | `LoveAlgo.Story` | `LoveAlgo.Modules.Audio` |
| AudioSettings.cs | `LoveAlgo.Story` | `LoveAlgo.Modules.Audio` |
| AudioModule.cs | `LoveAlgo.Modules.Audio` (유지) | (그대로) |
| IAudio.cs | `LoveAlgo.Modules.Audio` (유지) | (그대로) |

AudioModule.cs는 `using LoveAlgo.Story;` 제거 (AudioManager가 같은 ns로 옮겨오므로).

## 공개 표면 (이미 정의됨)

`IAudio` (변경 없음):
- `PlayBGM(string name, float fadeDuration = -1f)`
- `StopBGM(float fadeDuration = -1f)`
- `PlaySFX(string name)`
- `PlayVoice(string character, string voiceName)`
- `StopVoice()`

볼륨 설정(`SetBGMVolume` 등)은 `SettingsPopup`이 직접 `AudioManager.Instance`로 접근. 인터페이스에 추가 안 함 (cross-module 호출 아님).

## 호출자 13개

`AudioManager.` 또는 `AudioSettings` 참조 (`Modules/Audio/AudioModule.cs` 제외):

| 파일 | 비고 |
|------|------|
| `Story/ChoiceUI.cs` | `using LoveAlgo.Story;` 이미 있음 — 추가 |
| `Story/DialogueUI.cs` | 동일 |
| `Story/CharacterLayer.cs` | 동일 |
| `Story/CharacterSlot.cs` | 동일 |
| `Story/StoryEngine/ExecutionDependencies.cs` | 동일 |
| `Story/SaveSystem/SaveDataSerializer.cs` | 동일 |
| `Core/GameFlowController.cs` | `using LoveAlgo.Story;` 추가/확인 |
| `Core/SessionController.cs` | 동일 |
| `UI/SettingsPopup.cs` | 동일 |
| `UI/UsernameUI.cs` | 동일 |
| `UI/ToggleButton.cs` | 동일 |
| `UI/TitleUI.cs` | 동일 |
| `MiniGame/CherryBlossomGame.cs` | 동일 |

모든 호출자에서: `using LoveAlgo.Modules.Audio;` 추가. 기존 `using LoveAlgo.Story;`는 다른 Story 심볼(GameState 등) 사용 시 유지.

## 갱신 작업 (순서)

1. [ ] 폴더 생성: `Assets/_Project/Modules/Audio/Code/Events/`
2. [ ] 파일 이동 (`.cs` + `.cs.meta`):
   - `Scripts/Story/AudioManager.cs` → `_Project/Modules/Audio/Code/AudioManager.cs`
   - `Scripts/Story/AudioSettings.cs` → `_Project/Modules/Audio/Code/AudioSettings.cs`
   - `Scripts/Modules/Audio/AudioModule.cs` → `_Project/Modules/Audio/Code/AudioModule.cs`
   - `Scripts/Modules/Audio/IAudio.cs` → `_Project/Modules/Audio/Code/IAudio.cs`
   - `Scripts/Modules/Audio/Events/BGMChangedEvent.cs` → `_Project/Modules/Audio/Code/Events/BGMChangedEvent.cs`
3. [ ] 네임스페이스 변경:
   - `AudioManager.cs`: `namespace LoveAlgo.Story` → `namespace LoveAlgo.Modules.Audio`
   - `AudioSettings.cs`: 동일
4. [ ] AudioModule.cs 정리:
   - `using LoveAlgo.Story;` 라인 제거 (AudioManager가 같은 ns)
5. [ ] 호출자 13개 갱신: `using LoveAlgo.Modules.Audio;` 추가
6. [ ] 남은 `Scripts/Modules/Audio/` 빈 폴더 삭제 (.meta 포함)
7. [ ] 사용자 Unity 컴파일 확인

## 위험·주의

- **Resources.LoadAll<AudioClip>("Audio/SFX")** — 이동 안 함, 영향 없음
- **AudioSettings.asset (SO 인스턴스)** — `Resources/Data/AudioSettings.asset` 유지. 클래스 이동해도 GUID는 .cs.meta 따라가므로 OK
- **AudioManager 싱글톤 + DontDestroyOnLoad** — 인스턴스 동작 불변
- **Story 네임스페이스 의존** — AudioManager는 `GameState` (Story ns) 참조 안 함을 확인했으나, 다른 Story 심볼 사용 시 `using LoveAlgo.Story;` 추가 필요할 수 있음 (실행 시 발견)
- **CharacterDatabase 등 데이터 의존**: AudioManager는 캐릭터별 voice volume을 관리 → CharacterDatabase(Story ns) 참조하는지 추가 확인 필요

## 사용자 수동 확인

- [ ] Unity 에디터에서 `Assets > Reimport All` (선택, GUID 안정 확인용)
- [ ] 컴파일 0 error
- [ ] 런타임 검증: 타이틀에서 BGM 정상 재생, SFX 클릭음 동작

## 추정 토큰

Opus 직접 수행 시 ~30-40턴. Sonnet sub-agent ~50턴 가능 (호출자 13개 갱신이 길음).

이 레시피는 모범 검증용이므로 **Opus 본 세션에서 직접 수행**.
