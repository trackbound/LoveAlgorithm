# 코드 컨벤션

AGENTS.md의 MUST/MUST NOT이 진실 소스(source of truth).
이 파일은 빈번히 필요한 패턴만 정리.

## 비동기

```csharp
// 딜레이
await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);

// DOTween
await tween.ToUniTask(cancellationToken: ct);

// 병렬
await UniTask.WhenAll(task1, task2);

// 조건 대기
await UniTask.WaitUntil(() => condition, cancellationToken: ct);
```

## 새 기능 추가 패턴

- 팝업: `ModalPopupBase` 상속 → `PopupManager.ShowModal<T>()`
- 미니게임: `MiniGameBase` 상속
- UI 타입: `MainUIType` enum 추가 → `UIManager.ShowOnly()`

## 금지

- 코루틴 (`yield return`, `StartCoroutine`)
- `?.` 없는 싱글톤 접근
- GameState 세이브 포맷 변경
