# 사용자 입력 검증 (username + password) — 설계 스펙

- 날짜: 2026-06-20
- 위험도: 🟠 High (입력/세이브 직렬 대상 + IME 실시간 변환)
- 상태: 설계 승인 대기 → 구현 계획(writing-plans)

---

## 1. 배경 & 현황

**username**(플레이어 이름, `UsernameScreenView` → `GameStateSO.Data.playerName`, 세이브 직렬·`{{Player}}` 치환):
- `NameValidator`(LoveAlgo.Core)가 **이미 규칙을 구현**: 길이(최소2/한글6/영문12), 허용 패턴 `^[가-힣a-zA-Z0-9]+$`(완성형 음절만 → 미완성 자모 ㄱㄴㅇㄹ 자동 거부), 금칙어(`Resources/Data/BannedWords.txt` 로드, 존재·내용 채워짐), `Result`+`GetErrorMessage`.
- **문제**: `UsernameScreenView.Submit()`이 NameValidator를 **호출하지 않음** — 빈 값만 보고 바로 저장. 화면에 에러 표시 UI도 없음.

**password**(`PasswordInputField` — `LockScreenView`가 사용, `input.text`로 정답 대조):
- 현재 마스킹·길이(maxLength 7)·눈 토글·진동만. **문자셋 제한·한글→영문 변환은 없음**.

## 2. 목표 & 비목표

**목표**
- username: NameValidator를 Submit에 배선 — 무효 입력 거부 + 사유 표시 + 흔들림.
- password: 한글 입력을 **두벌식 QWERTY 역매핑**으로 영문 통일(실시간, 화면 표시 포함) + 영문/숫자/표준 ASCII 특수문자만 허용.

**비목표**
- NameValidator 규칙 자체 변경(길이·금칙어 목록은 현행 유지).
- password 정답/저장 스키마 변경(LockScreen 로직 불변 — 입력 텍스트만 영문화).
- 실시간 username 필터(한글 조합 간섭 회피 위해 on-submit 검증으로 한정).

## 3. 핵심 순수 로직 (신규)

**`HangulQwerty`** (`Assets/_Project/Scripts/Core/HangulQwerty.cs`, 정적, LoveAlgo.Core):

```csharp
public static class HangulQwerty
{
    /// 한글 입력을 두벌식 QWERTY 키로 역매핑하고, 영문/숫자/표준 ASCII 특수문자는 통과,
    /// 그 외(공백·제어·매핑 불가)는 제거한 문자열을 반환.
    public static string ToQwerty(string text);
}
```

- 문자별 처리:
  - **완성형 한글(U+AC00–U+D7A3, 가–힣)**: `(code-0xAC00)`을 초성(19)·중성(21)·종성(28)로 분해 → 각 자모를 QWERTY 시퀀스로 매핑해 이어붙임. 예: `가`=ㄱ+ㅏ→`rk`, `안`=ㅇ+ㅏ+ㄴ→`dks`, `값`=ㄱ+ㅏ+ㅄ→`rkqt`.
  - **호환 자모(U+3131–U+3163, ㄱ–ㅣ)**: 직접 매핑(ㄱ→`r`).
  - **ASCII 허용**(영문 대소문자 `A–Za–z`, 숫자 `0–9`, 표준 특수문자 `!"#$%&'()*+,-./:;<=>?@[\]^_`{|}~`): 그대로.
  - **그 외**(공백·제어·기타 유니코드): 제거.
- 복합 자모 분해 매핑:
  - 겹받침/쌍자음: ㄲ→`R`, ㄸ→`E`, ㅃ→`Q`, ㅆ→`T`, ㅉ→`W`; ㄳ→`rt`, ㄵ→`sw`, ㄶ→`sg`, ㄺ→`fr`, ㄻ→`fa`, ㄼ→`fq`, ㄽ→`ft`, ㄾ→`fx`, ㄿ→`fv`, ㅀ→`fg`, ㅄ→`qt`.
  - 이중모음: ㅘ→`hk`, ㅙ→`ho`, ㅚ→`hl`, ㅝ→`nj`, ㅞ→`np`, ㅟ→`nl`, ㅢ→`ml`; ㅐ→`o`, ㅒ→`O`, ㅔ→`p`, ㅖ→`P`.
- 두벌식 기준 단자모 매핑(요약): 자음 ㅂㅈㄷㄱㅅ→`qwert`, ㅁㄴㅇㄹㅎ→`asdfg`, ㅋㅌㅊㅍ→`zxcv`; 모음 ㅛㅕㅑㅐㅔ→`yuiop`, ㅗㅓㅏㅣ→`hjkl`, ㅠㅜㅡ→`bnm`.

> 매핑 테이블의 정확성은 §6 EditMode 케이스로 고정한다(대표 음절·복합자모·경계).

## 4. password 흐름 (`PasswordInputField` 수정)

- `OnEnable`에서 `input.onValueChanged`에 핸들러 등록(`OnDisable`에서 해제).
- 핸들러: `string converted = HangulQwerty.ToQwerty(input.text);` → `converted != input.text`일 때만 `input.text = converted; input.caretPosition = converted.Length;`(끝으로). **재진입 가드**(`_suppress` 플래그)로 무한루프 방지.
- 결과: 한글 IME로 쳐도 즉시 영문으로 보이고, 마스킹/`LockScreenView` 대조 모두 영문형 사용. 길이는 기존 `maxLength` 유지.

## 5. username 흐름 (`UsernameScreenView` 수정)

- 직렬화 필드 추가: `[SerializeField] TMP_Text errorLabel;`(미바인딩 시 메시지 생략).
- `Submit()`:
  1. `name = input.text.Trim()`. 빈 값은 기존대로 무시(입력 강제).
  2. `var r = NameValidator.Validate(name);`
  3. `r != Valid` → `if (errorLabel != null) errorLabel.text = NameValidator.GetErrorMessage(r);` + `UiNudge.Shake(this, inputRect)` + **저장/숨김 안 함**(return).
  4. `r == Valid` → 기존 저장(`state.Data.playerName = name`) + 숨김. (성공 시 errorLabel 비움.)
- **UI 추가**: username 화면 프리팹에 에러 텍스트(TMP) 1개 배치 + `errorLabel` 배선.

## 6. 공용 흔들림 추출

**`UiNudge`** (`Assets/_Project/Scripts/UI/UiNudge.cs`, 정적):
```csharp
public static class UiNudge
{
    /// rt를 수평으로 1버스트 감쇠 진동(코루틴은 host에서 구동). 종료 시 기준 위치 복원.
    public static void Shake(MonoBehaviour host, RectTransform rt,
        float amplitude = 12f, float frequency = 60f, float duration = 0.25f);
}
```
- `PasswordInputField.Shake`를 이 헬퍼 호출로 정리(기존 동작·수치 유지), `UsernameScreenView`도 사용. 코루틴은 `host.StartCoroutine`으로 구동(중복 호출 시 host가 관리).

## 7. 테스트

- **EditMode**
  - `HangulQwertyTests`: 대표 음절(`가`→`rk`, `안`→`dks`, `값`→`rkqt`), 호환자모(`ㄱ`→`r`), 복합모음(`와`=ㅇ+ㅘ→`dhk`), ASCII 통과(`Pass1!`→`Pass1!`), 한영 혼합, strip(공백/이모지 제거).
  - `NameValidatorTests`(보강): 비문(`ㄱㄴ`→InvalidCharacter), 금칙어(목록 단어 포함→BannedWord), 길이(한글7→TooLong, 1자→TooShort), 정상(`가나`→Valid).
- **PlayMode**
  - `PasswordInputFieldPlayModeTests`: `input.text="ㅂㅈㄷ"` 설정→onValueChanged→`"qwe"`; ASCII 유지; 한글 섞임 변환.
  - `UsernameScreenPlayModeTests`(보강): 무효 이름 Submit → 저장 안 됨(playerName 불변) + errorLabel 메시지 세팅 + overlay 유지; 유효 이름 → 저장+숨김.

## 8. 위험 & 완화

- 🟠 **실시간 IME 변환 간섭**: onValueChanged에서 text 교체가 한글 조합을 끊어 IME composition과 충돌할 수 있다. 플레이모드로 실검증하고, 불안정하면 폴백(`onEndEdit`/조합 확정 시 변환)으로 전환. 재진입 가드로 무한루프 방지.
- 두벌식 매핑 누락/오타 → EditMode 케이스로 대표·경계 고정. 매핑 불가 문자는 안전하게 제거(크래시 없음).
- username 에러 라벨 프리팹 배선 누락 → 메시지 생략(거부·미저장은 유지)로 graceful.

## 9. 파일 요약

- 신규: `Core/HangulQwerty.cs`, `UI/UiNudge.cs`, EditMode/PlayMode 테스트.
- 수정: `UI/PasswordInputField.cs`(onValueChanged 변환 + Shake→UiNudge), `UI/UsernameScreenView.cs`(NameValidator 배선 + errorLabel + shake), username 화면 프리팹(에러 라벨).
- 불변: `NameValidator.cs`, `BannedWords.txt`, LockScreen 로직.
