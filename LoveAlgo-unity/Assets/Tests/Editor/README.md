# LoveAlgo EditMode 테스트

Phase B2 회귀 안전망. `Assets/Tests/Editor/` 하위는 Unity가 자동으로 `Assembly-CSharp-Editor`로
컴파일하고 Test Runner가 NUnit attribute(`[Test]`, `[TestFixture]`)를 자동 인식한다.
`.asmdef`는 의도적으로 두지 않음 — Runtime 코드(`Assembly-CSharp`)를 직접 참조하기 위해
Editor 어셈블리의 implicit reference 동작에 의존.

## 실행

Unity 에디터 → `Window > General > Test Runner` → 상단 **EditMode** 탭 → **Run All**.

개별 파일/메서드 실행도 동일 패널에서 트리 선택.

## 헤르메틱

테스트가 실제 사용자 세이브를 건드리지 않도록:
- 슬롯 번호는 99 근방(`TestSlot1=91`, `TestSlot2=92`) — 일반 게임 슬롯(0~29) 범위 밖.
- 각 테스트의 `SetUp`/`TearDown`에서 해당 슬롯의 `.json`/`.json.bak`/`.json.tmp` 전부 삭제.
- `ScriptParser.Strict` 토글도 `TearDown`에서 기본값(false)으로 복원.

## 보류된 PlayMode 테스트

`Prologue smoke` (헤드리스로 프롤로그 스크립트 완주) 는 `ScriptRunner`가 `SingletonMonoBehaviour`라
씬 로드 의존 + 다른 모듈(Audio/Stage/UI)도 모두 부착돼 있어야 의미 있게 동작. 이를 위해서는
PlayMode test assembly가 Runtime을 참조해야 하는데, 현재 Runtime은 `.asmdef`로 분리되지
않아 PlayMode asmdef가 Runtime 코드를 참조 불가. **Phase C에서 모듈 .asmdef 도입 후 다시 시도**.
