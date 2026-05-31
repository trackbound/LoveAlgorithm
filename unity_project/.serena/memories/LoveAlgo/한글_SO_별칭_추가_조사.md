# Love Algorithm Unity — 한글 별칭 SO 추가 조사 보고

## 1. CharacterMetaDatabase 패턴 분석

**정의 위치**: `Assets/_Project/Modules/Narrative/Code/CharacterMetaDatabase.cs:1-80`

- **타입**: ScriptableObject (CREATE_ASSET_MENU: "LoveAlgo/Character Meta Database")
- **로드 방식**: Resources.Load<>("Data/CharacterMetaDatabase") — 싱글톤 (Instance 프로퍼티)
- **저장 위치**: `Assets/Resources/Data/CharacterMetaDatabase.asset`
- **구조**:
  ```csharp
  public class CharacterMeta {
    public string characterId;      // "Roa", "c01" 등 엔진 ID
    public string displayName;       // "로아" 한글 이름
    public List<string> speakerAliases;  // 추가 별칭 목록 (새 기능)
  }
  ```
- **한글→영문 매핑**: `SpeakerToCharacterId(speaker)` 메서드 (49-61줄)
  - displayName 먼저 조회, 없으면 speakerAliases 순차 조회
  - 대소문자 무시 (OrdinalIgnoreCase)

---

## 2. 리소스별 진입점 (CSV→엔진 처리)

| 리소스 | Executor | 진입점 | 파일:라인 | 처리 방식 |
|-------|----------|--------|----------|---------|
| **BG** | BGLineExecutor | `ExecuteAsync(line)` → `line.Value` 직접 파싱 | Handlers/BGLineExecutor.cs:17-18 | CSV Value(`bgName = parts[0]`)를 Background.ExecuteAsync() 전달 |
| **CG** | CGLineExecutor | (찾음: Handlers/CGLineExecutor.cs) | 미확인 세부 | line.Value → CG 레이어 전달 |
| **SD** | SDLineExecutor | `sd.ExecuteAsync(line.Value, ct)` | Handlers/SDLineExecutor.cs:33 | line.Value 파싱 없음, 직접 SDCutscene에 전달 |
| **Overlay** | OverlayLineExecutor | `overlay.ExecuteAsync(line.Value, ct)` | Handlers/OverlayLineExecutor.cs:20 | VirtualBGOverlay.ExecuteAsync() 호출 |
| **BGM/SFX** | SoundLineExecutor | `audio.ExecuteAsync(line.Value, ct)` | Handlers/SoundLineExecutor.cs:20 | AudioManager.ExecuteAsync() 위임 |
| **Char** | CharLineExecutor | `character.ExecuteAsync(value, ct)` | Handlers/CharLineExecutor.cs:27 | 한글 캐릭터명 → characterId 변환 없음 (런타임 미처리) |

**핵심**: 모든 Executor는 **CSV의 Value를 그대로 파싱**하며, 한글→영문 변환은 **StoryConvertWindow(에디터)에서만** 수행됨.

---

## 3. 폴더 구조 및 자산 위치

```
Assets/
├── Resources/Data/
│   ├── CharacterMetaDatabase.asset        ← 캐릭터 DB (Char 별칭)
│   ├── CharacterStageDatabase.asset       ← Stage 시각 표현
│   ├── VirtualOverlayDatabase.asset       ← 가상 캐릭터 오버레이 (Roa 등)
│   ├── EmoteMap.asset                     ← 표정 별칭 (런타임 로드)
│   ├── AudioSettings.asset
│   └── ...
└── _Project/Modules/Narrative/
    ├── Editor/Mappings/
    │   ├── BgMap.asset                    ← BG 매핑 (에디터만)
    │   ├── CgMap.asset                    ← CG 매핑
    │   ├── SdMap.asset                    ← SD 매핑
    │   ├── SoundMap.asset                 ← 음향 매핑
    │   └── (emote 레거시, 삭제됨)
    └── Code/
        └── CharacterMetaDatabase.cs
```

---

## 4. StoryConvertWindow (기획 CSV → 엔진 CSV)

**파일**: `Editor/StoryConvertWindow.cs`

- **변환 로직**: StoryCsvConverter.Convert() 호출 (125줄)
- **매핑 로드 시점**: OnEnable() → ReloadMaps() (47-59줄)
  - EmoteMap: Resources/Data에서 먼저, 없으면 Mappings/ fallback
  - CharacterMetaDatabase: Resources/Data만 (54줄)
  - BG/CG/SD/Sound: Mappings/ 폴더만 (에디터용)

**별칭 치환 위치**: `StoryCsvConverter.NormalizeByType()` (200-233줄)

### 각 Type별 변환:
- **Text** (줄기 스크립트): emote alias 치환만
- **Char** (캐릭터): 
  - **라인 296-305**: 한글 캐릭터명 → `opt.Meta.SpeakerToCharacterId(p)` 조회
  - 없으면 emote 매핑 시도, 그 외 그대로 통과
- **BG**: 한글 표기 → BgMap.TryResolve() (333줄)
- **CG/SD**: 액션 키워드만 처리, 리소스 ID는 매핑 안 함
- **Sound**: SFX:/ BGM: 프리픽스 정규화

---

## 5. 구현 권장사항

### **최적 위치: StoryConvertWindow (에디터 변환 단계)**
✓ 기획자가 기획 CSV에서 한글 별칭(예: "미소년로아")을 쓸 수 있음
✓ CharacterMetaDatabase의 speakerAliases 동적 참조
✓ 변환 결과가 엔진 CSV에 **확정 저장** (런타임 오버헤드 없음)
✓ BG/CG처럼 타입별 정규화 파이프라인과 일관성

### **현재 런타임**: CharLineExecutor에서 미처리
- 런타임 Char 명령(`Char:Enter:로아`)은 캐릭터명 미변환
- 만약 Char 명령의 동적 로드가 필요하면, CharLineExecutor에서 SpeakerToCharacterId() 호출 추가 (기존 Char/BG/SD와 패리티 깨짐)

---

## 핵심 코드 스니펫

**CharacterMetaDatabase.SpeakerToCharacterId()** (49-61줄):
```csharp
public string SpeakerToCharacterId(string speaker)
{
    foreach (var c in characters)
    {
        if (c.displayName.Equals(speaker, StringComparison.OrdinalIgnoreCase))
            return c.characterId;
        if (c.speakerAliases != null)
            foreach (var alias in c.speakerAliases)
                if (alias.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                    return c.characterId;
    }
    return null;
}
```

**StoryCsvConverter.NormalizeChar()** (274-315줄):
- 라인 296-305: `opt.Meta.SpeakerToCharacterId(p)` 호출 → 엔진 ID로 치환
