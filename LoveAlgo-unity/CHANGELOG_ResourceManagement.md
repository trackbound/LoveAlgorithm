# 리소스 관리 개선 — 변경사항 정리

> **작성일:** 2025-02-15  
> **대상 빌드:** 데모 빌드  
> **변경 범위:** 메모리 관리 / 에셋 라이프사이클 개선

---

## 배경 (왜 이 작업을 했는가)

기존에는 `Resources.Load()`로 불러온 에셋(배경, 캐릭터, CG, BGM 등)이 **한 번 로드되면 앱 종료까지 메모리에 영구 상주**하는 문제가 있었습니다.

- `Resources.UnloadAsset()` / `Resources.UnloadUnusedAssets()` 호출이 프로젝트 전체에 **0건**
- 캐릭터 스프라이트 캐시(`CharacterSlot.spriteCache`)가 **static**이고 절대 비워지지 않음
- CG(풀스크린 고해상도 이미지)가 숨겨진 후에도 메모리에 남음
- BGM 클립이 교체/정지 후에도 해제되지 않음

게임을 프롤로그부터 엔딩까지 플레이하면 사용된 모든 에셋이 누적되어 메모리 사용량이 계속 증가하는 구조였습니다.

---

## 변경 내역

### 1. 장면 전환 시 미사용 에셋 일괄 해제
**파일:** `Assets/Scripts/Core/GameManager.cs` — `CleanupStage()`

| Before | After |
|--------|-------|
| 레이어, 화면효과, 오디오 정리만 수행 | 추가로 캐릭터 캐시 클리어 + `Resources.UnloadUnusedAssets()` 호출 |

**동작:** 타이틀 복귀, 세이브 로드, 페이즈 전환(Title→Prologue→DayLoop→Ending) 시 참조 해제된 에셋이 실제로 메모리에서 해제됩니다.

```csharp
// 추가된 코드
CharacterSlot.ClearSpriteCache();
Resources.UnloadUnusedAssets();
```

---

### 2. 캐릭터 스프라이트 캐시 수명 관리
**파일:** `Assets/Scripts/Story/CharacterSlot.cs`

| Before | After |
|--------|-------|
| `static spriteCache`가 영구 유지 (절대 비워지지 않음) | `ClearSpriteCache()` public static 메서드 추가 |

**동작:** 5캐릭터 × 7표정 = 최대 35장의 대형 스프라이트가 장면 전환 시 캐시에서 해제됩니다. 다시 필요할 때 자동으로 다시 로드되므로 기능에는 영향 없습니다.

```csharp
// 추가된 메서드
public static void ClearSpriteCache()
{
    spriteCache.Clear();
    Debug.Log($"[CharacterSlot] 스프라이트 캐시 클리어");
}
```

---

### 3. CG 숨김 시 텍스처 즉시 해제
**파일:** `Assets/Scripts/Story/CGLayer.cs` — `HideAsync()`, `Clear()`

| Before | After |
|--------|-------|
| `cgImage.sprite = null`만 수행 (Unity 내부 캐시에 잔류) | 이전 CG의 텍스처를 `Resources.UnloadAsset()`으로 즉시 해제 |

**동작:** CG는 풀스크린 고해상도 이미지(수 MB)이므로, 숨겨지는 즉시 메모리를 반환합니다. 다시 표시할 때 자동 재로드됩니다.

```csharp
// HideAsync() / Clear() 내 추가된 코드
var oldSprite = cgImage.sprite;
// ... (기존 정리 로직) ...
if (oldSprite != null)
    Resources.UnloadAsset(oldSprite.texture);
```

---

### 4. BGM 클립 교체/정지 시 이전 클립 해제
**파일:** `Assets/Scripts/Story/AudioManager.cs` — `PlayBGMAsync()`, `StopBGMAsync()`, `StopBGMImmediate()`

| Before | After |
|--------|-------|
| 새 BGM 재생 시 이전 클립이 메모리에 잔류 | 이전 클립을 `Resources.UnloadAsset()`으로 해제 |
| BGM 정지 후 클립이 `bgmSource.clip`에 잔류 | 정지 후 `clip = null` + 해제 |

**동작:** BGM이 교체되거나 정지될 때, 더 이상 사용되지 않는 AudioClip이 즉시 해제됩니다.

```csharp
// PlayBGMAsync() 내 추가된 코드
var previousClip = bgmSource.clip;
// ... (재생 로직) ...
if (previousClip != null && previousClip != clip)
    Resources.UnloadAsset(previousClip);

// StopBGMAsync() / StopBGMImmediate() 내 추가된 코드
var clipToUnload = bgmSource.clip;
bgmSource.clip = null;
if (clipToUnload != null)
    Resources.UnloadAsset(clipToUnload);
```

---

## 수정된 파일 목록

| 파일 | 변경 내용 |
|------|----------|
| `Assets/Scripts/Core/GameManager.cs` | `CleanupStage()`에 캐시 클리어 + `UnloadUnusedAssets` 추가 |
| `Assets/Scripts/Story/CharacterSlot.cs` | `ClearSpriteCache()` 메서드 추가 |
| `Assets/Scripts/Story/CGLayer.cs` | `HideAsync()`, `Clear()`에 CG 텍스처 즉시 해제 추가 |
| `Assets/Scripts/Story/AudioManager.cs` | `PlayBGMAsync()`, `StopBGMAsync()`, `StopBGMImmediate()`에 이전 클립 해제 추가 |

---

## 기대 효과

- **메모리 누수 방지** — 게임 진행 중 에셋이 무한히 누적되지 않음
- **장시간 플레이 안정성** — 프롤로그→엔딩까지 메모리 사용량이 일정 범위 내 유지
- **기능 영향 없음** — 해제된 에셋은 다시 필요할 때 `Resources.Load`로 자동 재로드

---

## QA 체크리스트

- [ ] Play Mode에서 프롤로그 → Day1~3 진행 중 콘솔에 에러/경고 없는지 확인
- [ ] CG 표시 후 숨김 → 다시 표시 시 정상 표시되는지 확인
- [ ] BGM 전환(타이틀→프롤로그→데일리) 시 음악이 정상 전환되는지 확인
- [ ] 캐릭터 표정 변경(Default→Happy→Sad 등) 반복 시 정상 동작 확인
- [ ] 타이틀로 돌아간 후 다시 시작 시 모든 에셋 정상 로드 확인
- [ ] 세이브/로드 후 배경, 캐릭터, BGM 상태 정상 복원 확인
- [ ] Memory Profiler(`Window > Analysis > Memory Profiler`)로 Day 전환 전후 메모리 비교 권장

---

## 향후 개선 예정 (현재 규모에서는 불필요)

| 항목 | 도입 시점 |
|------|----------|
| **스크립트 프리로딩** (CSV 선행 파싱 → 비동기 에셋 프리로드) | CG 20장+ 또는 배경 50장+ 도달 시 |
| **중앙 리소스 캐시 매니저** (참조 카운팅 / LRU 방식) | 에셋 500개+ 또는 Addressables 전환 시 |
| **Addressable Asset System 도입** | DLC/패치 배포 필요 시 또는 빌드 사이즈 최적화 필요 시 |
