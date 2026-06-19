# 비밀번호 입력 커스텀 시스템 — 설계

> 작성일: 2026-06-19 · 위험도: 🟠 High (LockScreenView·Controller 인터페이스 확장 + 신규 이벤트 + 프리팹 구조)
> 슬라이스별 Git diff 검토 + 엣지케이스 자가검증 동봉.

## 1. 배경 / 목적

로아가 이름을 물어본 뒤 비밀번호 설정을 제안하며 진입하는 **잠금화면(로그인) 커스텀 시스템**.
영상(`03_login.mp4`)의 진입 연출(배경+위젯 → 위젯 슬라이드아웃 → 딤 페이드 → 비밀번호 입력)에 더해,
기획 스펙의 상태별 안내 텍스트 · 눈 토글 · 7자 제한 · 모드별 마스킹 · 오류 진동 · 3회+ 열쇠/재설정까지 포함한다.

현재 구현은 `LockScreenView`(오버레이+입력+엔터/Confirm)와 `LockScreenController`(FirstSetup 평문 저장+완료 핸들)의
**최소 골격**만 있다(ADR-007 완료-핸들, ADR-013 첫 Overlay). 본 설계는 이를 가산적으로 확장한다.

### 기존 테스트 계약 (반드시 보존)
`LockScreenPlayModeTests` 기준:
- `ShowLockScreenCommand` 수신 시 `LockScreenView`가 **동기적으로** `overlay`를 활성화.
- `Confirm()`이 `SubmitPasswordCommand` 발행 + 닫기, 빈 입력은 무시(재입력).
- View 테스트는 director/위젯 없이 view만 배선 → **연출은 선택적(가산) 구조**여야 한다.

## 2. 아키텍처 / 컴포넌트 (책임 분리)

### 뷰 레이어 (`Assets/_Project/Scripts/UI`, 표시+명령만 / ADR-007)
- **`LockScreenView`** (수정) — Show 수신·overlay 동기 활성·Confirm→Submit·Hide. 테스트 계약 유지.
  모드(Setup/Normal/Reset)에 따라 하위 위젯 구성을 설정하고 진입 연출을 IntroDirector에 위임.
- **`LockScreenIntroDirector`** (신규) — 진입 연출 전담: 위젯 hold → 위젯별 슬라이드아웃(가까운 화면 밖, ease-in)
  → Dim 0→0.58 페이드 → 입력/버튼 reveal → `onInputReady` 콜백. 뷰가 위임, **미바인딩 시 폴백**(기존 즉시 경로).
  직렬화: 슬라이드 위젯 리스트 `{RectTransform, exitOffset, AnimationCurve}`, Dim Image, 입력/버튼 CanvasGroup, 타이밍.
- **`PasswordInputField`** (신규 소형) — TMP_InputField 래핑: 7자 제한, 모드별 마스킹 기본값,
  **눈 토글**(감은눈=★ 마스킹 / 뜬눈=평문), **오류 진동(shake)** 애니메이션.
- **`LockScreenGuideText`** (신규 소형) — 입력칸 위 안내 텍스트. 상태별 문구를 **인스펙터 직렬화**(설정/설정완료/입력/분실).
  뷰가 상태를 전환(`SetState(GuideState)`).
- **`LoginButton`** (신규 소형) — 모드별 라벨("입력 완료"/"LOGIN") + active/deact 스프라이트 토글(입력 유무) + 클릭→`view.Confirm()`.
- **`KeyResetButton`** (신규 소형, S3) — 3회+ 오류 시 우하단 등장 → 클릭 시 `ShowModalCommand`(예/아니오)
  → 예→`RequestPasswordResetCommand` 발행.

### 로직 레이어 (`Scripts/Narrative` Flow, 의미/상태)
- **`LockScreenController`** (수정) — Show 보관(mode/handle) + Submit 처리:
  - FirstSetup/Reset: 비번 평문 저장 → 핸들 완료.
  - Normal: 저장 비번과 대조. 일치→핸들 완료(로그인). 불일치→`PasswordVerifyFailedEvent` 발행(횟수 동봉), 핸들 유지.
  - 오류 횟수는 **세션 런타임**(컨트롤러 보유), 세이브 비저장. Reset 진입 시 0으로 리셋.

## 3. 신규 이벤트 (`Core/Events/LockScreenEvents.cs`)
- `PasswordVerifyFailedEvent { int ErrorCount }` — Controller→View (진동 + 가이드/열쇠 갱신).
- `RequestPasswordResetCommand` — KeyResetButton(예 선택)→Controller (모드를 Reset=FirstSetup 재진입으로 전환).
- 재설정 확인 팝업: **기존 `ShowModalCommand`/`ModalRequest`(Yes/No 버튼 종류) 재사용** — 신규 모달 인프라 없음.

## 4. 상태 머신 (모드별 동작)

| 모드 | 눈 기본값 | 마스킹 | 버튼 라벨 | 가이드 텍스트 |
|---|---|---|---|---|
| FirstSetup | 뜬눈(평문) | 안 함 | "입력 완료" | "앞으로 사용할 비밀번호를 입력해주세요.\n최대 7자까지 입력 가능합니다." |
| (제출 직후) | — | — | — | "비밀번호 설정 완료!" |
| Normal | 감은눈(★) | 함 | "LOGIN" | "비밀번호를 입력해주세요." |
| Normal 오류<3 | (유지) | 함 | (유지) | 진동만 (텍스트 유지) |
| Normal 오류≥3 | (유지) | 함 | (유지) | "비밀번호를 잊으셨다면 우측 하단 열쇠 모양 버튼을 눌러주세요." + 우하단 열쇠 등장 |
| Reset | =FirstSetup | 안 함 | "입력 완료" | =FirstSetup |

- 공통: 최대 7자, 문자 제한 없음. 눈 토글은 모든 모드에서 동작(**기본값만 모드별**).
- Reset은 UI상 FirstSetup과 동일(평문 기본·"입력 완료"·설정 가이드). 기획서 "8페이지로 재이동 = 재설정".

## 5. 데이터 흐름

```
Flow → ShowLockScreenCommand(mode)
     → View.OnShow(동기 overlay 활성)
     → IntroDirector.Play() [위젯 hold → 슬라이드아웃 → Dim 페이드 → 입력/버튼 reveal] → onInputReady
     → 모드별 위젯 구성(눈 기본값/마스킹/버튼라벨/가이드)
     → 사용자 ★/평문 입력(≤7자) → 엔터 또는 LoginButton
     → View.Confirm → SubmitPasswordCommand → Controller
```
- **FirstSetup**: 저장 → "설정 완료!" → 핸들 완료 → Hide(FadeOut, 검은 화면 노출까지 페이드).
- **Normal 불일치**: `PasswordVerifyFailedEvent`(횟수) → View 진동 (+≥3 열쇠/분실 가이드). 핸들 유지(재입력).
- **열쇠 클릭** → `ShowModalCommand`(예/아니오) → 예 → `RequestPasswordResetCommand` → Controller가 Reset 재진입.
  아니오 → 팝업만 닫고 잠금화면 유지.

## 6. 슬라이스 (각 = 1 Atomic Commit)

- **S1 — 진입 연출 + FirstSetup 해피패스**
  `LockScreenIntroDirector`(위젯·슬라이드·딤) + `LockScreenGuideText` + `PasswordInputField`(7자·눈토글·FirstSetup 평문)
  + `LoginButton`("입력 완료") + "비밀번호 설정 완료!" 전환. 프리팹 위젯 PNG 충실 배치 포함.
  위젯 슬라이드 대상 = {WARNING, audio, TODO, ROA message box, (header 판단)}. 시계 23:58·Background는 잔존(딤 위).
- **S2 — Normal 로그인 + 검증**
  Controller Normal 분기(저장 비번 대조), 마스킹 기본(감은눈), 일치→완료/불일치→`PasswordVerifyFailedEvent`+진동.
  가이드 "비밀번호를 입력해주세요.".
- **S3 — 오류/분실/재설정**
  3회+ 오류 → 우하단 `KeyResetButton` 등장 + 분실 가이드 → 클릭 시 `ShowModalCommand`(예/아니오)
  → 예→`RequestPasswordResetCommand`→Reset 재진입, 아니오→유지.

## 7. 테스트

- **기존 PlayMode 4종 유지** (IntroDirector 미바인딩 폴백으로 동기 overlay/Confirm 계약 보존).
- **S1**: `PasswordInputField` 7자 제한·눈 토글(★↔평문)·FirstSetup 평문, `LockScreenGuideText` 상태 전환,
  `LockScreenIntroDirector` `onInputReady` 콜백 도달 + Dim 최종 alpha 도달.
- **S2**: Controller Normal 일치→핸들 완료 / 불일치→`PasswordVerifyFailedEvent`(횟수 누적).
- **S3**: ≥3 오류 시 `KeyResetButton` 노출, 모달 예→`RequestPasswordResetCommand`→Reset 재진입, 아니오→유지.

## 8. 비범위 (YAGNI)

- 비밀번호 해싱(현 평문 저장 유지 — 후속). 다국어. 소프트 키보드 커스텀.
- 영상에 없는 password 보기/숨김 외 추가 토글. header.png 용도는 S1 빌드 시 이미지 확인 후 확정.

## 9. 미해결 / 빌드 시 확정
- `header.png`의 정체(상단 바 vs 위젯) — S1 프리팹 빌드 때 이미지 확인 후 잔존/슬라이드 분류.
- Normal 오류<3 구간의 별도 안내 문구는 스펙에 없음 → 진동만, 텍스트는 "비밀번호를 입력해주세요." 유지.
- IntroDirector 타이밍 기본값(hold/slide/dim/reveal)은 영상(~1.8s) 기반 초기치 후 감독 튜닝.
