# 🔑 HANDOFF — 재작성 세션 진입점 (LoveAlgorithm)

> 이 문서를 읽는 Claude에게: LoveAlgorithm 코드베이스를 **EventBus + ScriptableObject 단일 패턴으로 전체 재작성** 중이다.
> 대화를 재현하려 하지 말고, 아래 결론·금지선·다음 액션만 지켜라.
> 감독은 CS 전공 + Unity/C# 베테랑. Unity 기초 설명 금지, 동료 개발자처럼 설계 근거 중심으로.

---

## ⚡ 30초 요약

- **프로젝트**: LoveAlgorithm — Unity 6 + URP 2D 비주얼노벨/연애 시뮬. 5히로인·30일 루프·CSV 스토리 엔진.
- **지금**: **코드 전체 재작성**(아트/프리팹 유지). 아키텍처를 Service Locator → **EventBus + SO 단일**로 전환.
- **브랜치**: `rewrite/eventbus-so`(작업) / `wip/pre-rewrite-snapshot` @ 9ac3c9e(재작성 전 WIP 보존) / `main` b40964b.
- **기준 문서**: `docs/REWRITE_FEATURE_INVENTORY.md`(기능·공식·수치) · `REWRITE_CLASS_MANIFEST.csv`(클래스 처리 체크리스트) · `REWRITE_TUNING_VALUES.csv`(연출 수치 동결) · `docs/decisions.md` ADR-007~012.
- **환경**: Unity 에디터 + MCP(`mcp__UnityMCP__*`)가 작업트리를 본다. recompile/콘솔/테스트(`run_tests`) 가능.
  - ⚠️ **MCP는 세션 시작 시 에디터가 떠 있어야** 붙는다(stdio). 안 붙으면 `/mcp` 재연결 또는 헤드리스 배치(`Unity 6000.4.3f1 -batchmode -runTests -testPlatform EditMode`, 에디터 닫힌 상태에서만). 임시 산출물(log/xml)은 커밋 전 삭제.
  - ⚠️ **MCP `execute_code` 사용 불가**(Roslyn 미설치+DLL 수 커맨드라인 한계), **헤드리스 스크린샷 백지**(포커스 없는 Game View). 런타임 검증은 **PlayMode 테스트**로, 시각 확인은 감독이 직접 Play.

---

## 🚫 금지선

1. **추측 금지** — 기능·수치 모르면 인벤토리/코드 확인 또는 **질문**. (이번 세션 Shop 슬라이스2에서 적용: 동결값/스키마 인접 결정은 멈추고 질문.)
2. **호감도 공식·수치 변경 금지** — 인벤토리 §4 그대로(임계치 로아46/하예은32/서다은35/이봄39/도희원43 등).
3. **아트/프리팹/씬/SO GUID 보존** — `.meta` 건드리지 말 것(=`git mv`로 이동). 코드만 재작성.
4. **Service Locator / 인터페이스 계약(`I*`) 부활 금지** — EventBus + State SO만 (ADR-007).
5. **매니저 4개 초과 금지** — GameManager/AudioManager/SaveManager/UIManager (4개 골격 완성됨).
6. **SO 상태 영구화 금지** — 런타임 상태는 부팅 리셋 + 세이브 직렬화(Definition/Instance 분리).
7. **과설계 게이트** — "나중에 쓸지도"면 만들지 말 것.

---

## ✅ 확정 (ADR 근거)

- 아키텍처: EventBus(통지·명령) + State SO 직접 읽기(동기 GET) + 완료-이벤트(await 케이스). (ADR-007)
- 전체 재작성·아트 보존·`rewrite/eventbus-so`(ADR-008) / 내러티브 자체 CSV 엔진(ADR-009) / 위험도 게이트+마일스톤+커밋"왜"(ADR-010) / 코드 `_Project/Scripts/` 피처별 asmdef(ADR-011) / 연출 수치 SO화(ADR-012).
- **순수층 + 얇은 어댑터 패턴(정착됨)**: 공식/결정 로직 = 순수 static(`*Service`/`*Formula`/`*Interpreter`, GameStateSO 인자, EventBus 무관, EditMode 테스트). EventBus 연결 = 얇은 MonoBehaviour 어댑터(`*Controller`/`*Manager`, OnEnable 구독→순수 호출→통지 발행). 새 기능은 이 패턴을 따른다.
- **asmdef 9개**: `Core ← Data ← {Affinity, Schedule, Game}`, `Core ← {Save, Audio, Shop}`, `Narrative ← {Core, Affinity}`, `UI ← {Core, TMP}`. + 테스트 `LoveAlgo.Tests.EditMode/PlayMode`. **`Save`·`UI`만 autoReferenced=false**(구 동명 ns 충돌 회피), 나머지 true. 전부 옛 Assembly-CSharp과 공존.

---

## 위험도 게이트 (작업 착수 시 등급 선언)

| 등급 | 대상 | 리뷰 |
|---|---|---|
| 🔴 Critical | EventBus, SaveData 스키마, State SO, 씬 흐름 | 감독 정독+승인 |
| 🟠 High | 모듈 경계, 세이브 마이그레이션 | 설계+diff |
| 🟡 Medium | 모듈 내부 로직 | 작동증거+diff 훑기 |
| 🟢 Low | SO 에셋, UI 트윈 | 작동 테스트만 |

---

## 📍 현재 상태 (한눈에)

**엔드투엔드 시뮬레이션 루프가 섰다** — 골격(4매니저+순수/공식층+슬라이스1) 위에 실 게임 씬 `Assets/_Project/Scenes/Game.unity`가 신 매니저들로 돈다: 부팅→스케줄 선택(실 UI)→행동 소진→하루 전환→오토세이브→반복→30일 엔딩. 각 슬라이스 EventBus+SO 패턴, 항상 컴파일, EditMode+PlayMode 테스트 통과. **현재 EditMode 228 / PlayMode 49 그린, 컴파일 0에러.** (슬라이스별 상세 = git log.)
**+ M3 내러티브 런타임 슬라이스1(대사+선택지) 코드+씬 배선 완성** — CSV를 받아 대사 표시→선택지→효과→점프→종료까지 코루틴으로 구동(아래 표). `Game.unity`에 실제 배치됨(아래 ⚠️ 씬 구조).
**+ M3 슬라이스2 스테이지 BG+Char 런타임 — 코드+테스트+씬 배선 완성(커밋 b967218·6c87766).** `Game.unity`에 `_Stage` 캔버스(Screen Space-Overlay, sortingOrder −10) 1차 배선됨: Background/Front·Back + Characters/SlotL·C·R + StageView 전 바인딩 + NarrativeController.stageTuning + 데모 CSV(BG/Char). DevNarrativeButton으로 Play 확인 가능, 슬롯 레이아웃은 감독 튜닝 영역. 순수 StageInterpreter(BG/Char Value→인텐트) + StageEvents(enum/intent/StageRequest 완료핸들/Show*Command, Core) + StageTuningSO(동결 수치 Definition, `Resources/Data/StageTuning.asset`) + StageView(UI, 코루틴 lerp BG 크로스페이드/Cut/Fade·Char 슬롯 L/C/R 페이드, 컨벤션 로딩 `BG/{name}`·`Characters/{id}_{emote}`, NarrativeFinished→ClearAll) + NarrativeController BG/Char 케이스(파싱→duration해석→발행→Next대기). EditMode+5·PlayMode+5 그린. 씬 배선 가이드 = docs/HANDOFF_NOTES.md. **+ Sound(BGM/SFX/Voice) 서브슬라이스 완성(커밋 c48f10c)**: 순수 SoundInterpreter(Sound Value→SoundIntent) + NarrativeController Sound 케이스(기존 오디오 명령 발행, AudioManager 구독·재생 — 뷰/이벤트/SO 신설 없음). EditMode+8. 데모 CSV에 SFX 2개(BGM은 Resources/Audio/BGM 부재로 보류). **+ 스크린 FX(FadeOut/FadeIn/Flash) 서브슬라이스 완성(커밋 2086ad3·27f5af6)**: 순수 FxInterpreter(스크린 3종만 유효, 나머지 FX skip) + ScreenFxEvents + ScreenFxTuningSO(페이드0.9/플래시0.14) + ScreenFxView(최상위 `_ScreenFx` 캔버스 sortingOrder 100, 전체화면 오버레이 알파 lerp, NarrativeFinished 리셋) + NarrativeController FX 케이스(WaitStage→WaitNext 일반화로 Stage/FX 공유). 데모 CSV에 Flash·FadeOut. EditMode+5·PlayMode+4. **남은 FX**: 카메라(CamShake/Zoom/Pan/Reset)·아이마스크(Eye*)·색(ColorTint)·흔들기(Stage/Dialogue/Char)·캐릭터(CharJump/Dim/Glitch)·매크로(Setup/Day*/Scene*/Wait). **+ 오토모드(자동 진행) 완성(커밋 5c18dfd)**: SetAutoModeCommand → DialogueView가 클릭 대기를 지연-자동진행으로(클릭 시 즉시). 토글 UI/설정 영속화는 후속. PlayMode+2. **+ 자가감사 정리(커밋 2666b9f)**: 완료 핸들 3종→공용 `CompletionHandle` 통합(기능별 복제 금지), 순수 값-파서는 `*Parser` 네이밍(Stage/Sound/Fx, ChoiceParser 정합), StageView 과설계(ISerializationCallbackReceiver) 제거. **컨벤션 확정**: 완료 대기는 `CompletionHandle`(값 회수는 ChoiceRequest), 순수 파싱=`*Parser`·상태/실행=`*Interpreter`. **+ 인라인태그 `<wait:sec>` 완성(커밋 1943621)**: 순수 InlineTagParser(표시텍스트+InlinePause[]) + ShowDialogueCommand.pauses + DialogueView 타이핑 멈춤(타이핑 기준 문자열길이로 변경). `<emote>`는 화자→슬롯 해석 필요해 제외(제거만). EditMode+8·PlayMode+1. **+ 흔들기 FX(StageShake/DialogueShake/CharShake) 완성(코드+테스트+SO+씬배선)**: 순수 ShakeParser(구 ParseShakeArgs 의미 1:1 — Stage/Dialogue=[지속][강도프리셋/숫자], Char=[슬롯][숫자강도][지속]) + ShakeEvents(ShakeTarget enum·ShakeProfile·ShakeCommand 완료핸들, Core) + ShakeTuningSO(프리셋 10/25/50px·지속0.3·대상별 임팩트 프로파일 동결, `Resources/Data/ShakeTuning.asset`) + ShakeView(대상별 1개, 임팩트 감쇠 진동 코루틴 Hitlag→exp·sin, 핸들 완료·NarrativeFinished 리셋, DOTween 미사용) + NarrativeController PlayFx 스크린→흔들기→스킵 분기. **감독 결정**: 진동수/감쇠는 동결문서(perlin 25Hz/2.2)가 아닌 실제 런타임 임팩트 모델(Stage 5Hz/5.2·Dialogue 6Hz/6.5) 동결, Char는 구 DOTween을 임팩트 모델로 통일. **씬 배선(Game.unity)**: `_Stage`에 Content 래퍼 신설(Overlay 캔버스라 루트 이동 불가 → Background+Characters 재배치) + ShakeView(Stage) / DialogueView·Characters에 ShakeView(Dialogue/Char, 슬롯 L·C·R 배선) + NarrativeController.shakeTuning 바인딩 + 데모 CSV 흔들기 3종. EditMode+10·PlayMode+5. **+ 카메라 FX(CamZoom/CamPan/CamReset, +CamShake 별칭) 완성(코드+테스트+SO+씬배선)**: 순수 CameraFxParser(CamZoom[:배율[:지속]]·CamPan:x:y[:지속]·CamReset[:지속], 구 stageTransform DOScale/DOAnchorPos 의미 1:1) + CameraFxEvents(CameraFxKind enum·CameraFxCommand 완료핸들, Core) + CameraFxTuningSO(줌0.5·팬0.5·리셋0.4s 동결, `Resources/Data/CameraFxTuning.asset`) + CameraFxView(`_Stage/Content` 래퍼의 localScale/anchoredPosition 코루틴 lerp, InOutCubic/리셋 OutCubic 이징 보존, 지속상태·NarrativeFinished 원복) + NarrativeController PlayFx 카메라 분기. **UI 무대엔 월드 카메라 없음 → 카메라=Content 트랜스폼 조작**(흔들기 슬라이스의 Content 래퍼 재사용). **CamShake는 ShakeParser에 흔들기 가족으로 추가**(ShakeTarget.Stage). 세이브 스키마 무관(스테이지 세이브에 카메라 없음). EditMode+9·PlayMode+5. **+ 색 틴트 FX(ColorTint) 완성(코드+테스트+SO+씬배선)**: 순수 ColorTintParser(ColorTint:프리셋[:alpha[:dur]]·Clear, 구 ParseTintColor 의미 1:1, 미지정 프리셋=해제) + ColorTintEvents(ColorTintCommand RGB분리 완료핸들, Core) + ColorTintTuningSO(프리셋 6색 Sepia/Blue/Red/Pink/Green/Sunset·알파0.25·지속0.5s 동결, `Resources/Data/ColorTintTuning.asset`) + ColorTintView(전체화면 오버레이 색 코루틴 lerp, 지속상태·Clear=알파0·NarrativeFinished 해제) + NarrativeController PlayFx 틴트 분기(프리셋→색은 컨트롤러가 SO로 해석해 RGB로 발행 → 뷰가 SO 모름). **배치 결정(재설계)**: 구는 화면 최상위(대사 포함) 틴트였으나, 재작성은 **스테이지 레벨**(`_Stage`에 TintOverlay → bg+캐릭터만 틴트, 대사/HUD 깨끗, `_ScreenFx` 페이드가 자연히 위) 채택 — 화면 전체 원하면 TintOverlay를 `_ScreenFx`로 재배치 1줄. EditMode+7·PlayMode+3. **+ 아이마스크 FX(EyeClose/EyeOpen/EyeCloseImmediate/EyeBlink) 완성(코드+테스트+SO+씬배선)**: 순수 EyeMaskParser + EyeMaskEvents(EyeMaskAction enum·EyeMaskCommand, Core) + EyeMaskTuningSO(감기/뜨기 0.8s·깜빡 0.1/0.15/0.05 동결, `Resources/Data/EyeMaskTuning.asset`) + EyeMaskView(상/하 검은 바 anchoredPosition 보간, **구 2단계 peek 안무·이징 곡선 안 베끼고** 일반 VN 표준 단일 SmoothStep·대칭 슬라이드로 재설계, 닫힘정도 t 단일 보간, NarrativeFinished 뜨기) + NarrativeController PlayFx 아이마스크 분기. 씬: `_ScreenFx`에 EyeBarTop/Bottom(검정, 부팅 inactive) + EyeMaskView(부모 캔버스 높이로 바 지오메트리 자동 설정). **세이브 영속(IsEyeClosed)은 FX-only로 defer** — 현재 재작성엔 스테이지 상태 세이브(BG/Char/Tint 등)가 전무하므로 Eye도 동일, 향후 스테이지-상태-세이브 슬라이스에서 일괄. EditMode+8·PlayMode+4. **+ 스테이지 레이어 CG/SD/Overlay 완성(코드+테스트+SO+씬배선)**: 셋은 문법·동작(이미지 레이어 페이드 인/아웃/닫기)이 동일하고 차이는 z-위치+CG 결합뿐이라 **공유 추상화**(과설계 게이트: near-duplicate 3벌 금지) — 순수 StageLayerParser(`name[:transition[:dur]]`/`Close·Exit·Hide·FadeOut`, Kind는 LineType에서 주입) + StageLayerEvents(StageLayerKind/LayerTransition enum·ShowStageLayerCommand·**SetCgModeCommand**, Core) + StageLayerTuningSO(CG/SD/Overlay fade 0.5s 동결, `Resources/Data/StageLayerTuning.asset`) + StageLayerView(종류별 Image 3슬롯, 컨벤션 로딩 `CG·SD·Overlay/{name}`, 알파 lerp·Cut, CG 진입/종료 시 SetCgModeCommand 발행) + NarrativeController Run에 CG/SD/Overlay 케이스. **CG 결합(감독 결정: 이벤트 토글)**: CG 진입 시 StageLayerView가 SetCgModeCommand(true) 발행 → DialogueView(루트 숨김)·StageView(charContainer 토글)가 구독해 대사창·캐릭터 숨김, 종료 시 복원(ADR-007 디커플). 씬: `_Stage`에 OverlayLayer/SDLayer/CGLayer + StageLayerView + StageView.charContainer=Characters 배선. **z-배치**: 일단 Content(BG+캐릭터) 위에 배치 — Overlay가 캐릭터 위/아래인지는 디자인 의도라 감독 튜닝 영역(🟢, 슬롯 레이아웃과 동일). 셋의 문법 동일성 때문에 3커밋 분할 대신 1슬라이스로 통합(감독 승인 옵션의 분할 로지스틱은 공유파일이라 비효율 → DRY 우선). EditMode+7·PlayMode+4. **그 외 다음 서브슬라이스**: 점프페이드/스테이지합성·선택지 조건(플래그 평가)/이력(⚠️choiceHistory=세이브 스키마 🔴)·`<emote>` 인라인(화자→슬롯 해석 필요)·**스테이지 상태 세이브**(BG/Char/Tint/Eye/CG 등 일괄 영속화, 🔴 세이브 스키마). (구조는 요구사항에서 설계 — 워크플로우 규율 '재설계≠전사' 준수.) 별칭/카탈로그(한글명→ID, Default→코드)는 컨벤션 로딩에서 승격 필요. **LockScreen은 보류** — 구 모듈이 Services/UniTask 의존이라 rewrite 씬에 못 넣음(금지선#4) → ADR-013 Overlay로 재작성(페이즈머신 의존).

| 영역 | 상태 | asmdef |
|---|---|---|
| Core 인프라 (EventBus·Log·MoneyFormat·NameValidator·Hangul·Headless) | ✅ | Core |
| 상태/세이브 스키마 (GameStateData/SO·SaveData·JsonSaveStore) | ✅ | Core |
| Data/공식 (GameBalanceSO·GameConstants·GameTimeline·DayLoop) | ✅ | Data |
| 호감도 공식 (AffinityFormula §4 1:1) | ✅ 순수 | Affinity |
| Schedule (Effects·Service·Controller) | ✅ 통합 | Schedule |
| Narrative 파서/검증/Flow (Parser·Validator·FlowCommandInterpreter·FlowCommandController) | ✅ 순수+컨트롤러 | Narrative |
| **Narrative 런타임 슬라이스1** (대사+선택지: ScriptCursor·ChoiceParser·ChoiceEffectInterpreter 순수 + NarrativeController 어댑터 + DialogueView·ChoiceView·ChoiceSlot) | ✅ **코드+테스트** | Narrative/UI |
| 매니저 GameManager(하루전환)·SaveManager(오토세이브)·AudioManager(재생)·UIManager(그룹) | ✅ 슬라이스1 | Game/Save/Audio/UI |
| HUD (Day/Money/Affinity/Stat/Status) | ✅ 슬라이스1·2 | UI |
| Shop (구매+Consumable+SessionBuff 즉시가산+중복50%페널티) | ✅ 슬라이스2 | Shop |
| 부팅 (GameBoot·GameBootstrap 컴포지션 루트) | ✅ 완성 | Game |
| **실 게임 씬 시뮬레이션 루프** (부팅+4매니저+ScheduleController+HUD+ScheduleView+EndingView) | ✅ **엔드투엔드** | `Assets/_Project/Scenes/Game.unity` |
| 스케줄 선택 UI (ScheduleView·ScheduleSlot, 슬롯 클릭→명령) | ✅ | Schedule(피처 응집) |
| 엔딩 화면 (EndingView, 30일 종료점) | ✅ 최소 | UI |
| 통합 dev 씬 (전 매니저 EventBus 협업 + HUD 시각화) | ✅ | `Assets/_Dev/Integration/IntegrationTest.unity` |

**아직 안 된 것 / 다음 우선순위 후보**:
- **✅ 엔드투엔드 시뮬레이션 루프 해소**(이번 세션): `Game.unity`가 신 매니저로 실제 플레이된다(부팅→스케줄선택→하루전환→오토세이브→30일 엔딩). 단 **내러티브(대사/선택지)는 제외** — 시뮬레이션 페이즈만. 구 `Main.unity`는 페이즈2(`9152615`)에서 삭제됨.
- **🟢 HUD/슬롯 시각 레이아웃 미조정**: 기능 배선만 됨(위치/폰트/스타일은 감독이 Play로 다듬는 영역). 엔딩 결과 디테일(최고 호감도 등)도 최소.
- **M3 내러티브 런타임 — 슬라이스1(대사+선택지) 완성 + 씬 배선 완료**: 완료-핸들 커맨드 패턴으로 엔진↔UI 디커플(ADR-007). 순수 ScriptCursor/ChoiceParser/ChoiceEffectInterpreter(EditMode 18) + NarrativeController 코루틴 어댑터 + DialogueView/ChoiceView(PlayMode 2). 선택지 `Love:`는 Affinity 카테고리(`Affinity:Point:Id:Dialogue:N`)로 위임→정본 단일화(감독 결정). `Game.unity`에 실배치(매니저 2 + Narrative UI 그룹 + dev 트리거 버튼). **남은 것**: 슬라이스2(스테이지 Char/BG/CG/SD/Overlay·FX·Sound·오토모드·인라인태그·점프페이드/스테이지합성/로그복원·선택지 조건/이력·LockScreen 계열 Flow) + **실 트리거**(이벤트→스크립트 매핑, dev 버튼 대체) + **엔진 포맷 스토리 CSV**(현재 기획 CSV만 존재).
- **Shop 슬라이스2(감독 결정 필요)**: SessionBuff 적용 경계(구 코드: 다음 스케줄 base효과 직후) / Gift 인벤토리(🔴 세이브 스키마) / 중복 50% 페널티(상태 위치).
- **GameManager 잔여 seam**: 저녁이벤트(M3)·페이드(M5 UI)·페이즈전환(GamePhase 상태머신). (오토세이브 seam은 완료.)
- **Settings**(볼륨↔AudioMixer = AudioManager 슬라이스2) / **구 모듈 폐기**(소비처 이식 완료 시 Service Locator·구 매니저 제거).

---

## ⚠️ 새 세션이 반드시 알 것 (안 깨뜨리려면)

- **~~전환기 공존~~ → 구 코드 폐기 완료(`9152615`)**: 구 Assembly-CSharp(Service Locator·구 매니저·Modules·Contracts·구 UI)는 **전부 삭제됨**. 코드는 이제 `_Project/Scripts/` 피처 asmdef 12개 단일이고, Assembly-CSharp에는 서드파티(DOTween)·Unity생성(InputSystem_Actions)만 잔존(게임코드 아님).
- **~~autoRef=false 2개~~ → true 복귀 완료(`9152615`)**: 구 동명 ns 충돌이 사라져 `LoveAlgo.Save`·`LoveAlgo.UI` 모두 `autoReferenced: true`(이제 asmdef 12개 전부 autoRef true). 컴파일·테스트 그린 확인.
- **이름 충돌 패턴**: 신규 타입이 구 동명 타입과 겹치면 → 다른 ns(예: 구 Modules.Audio vs 신 LoveAlgo.Audio) 또는 asmdef 격리(autoRef=false, 예: UIManager). **MCP로 씬에 컴포넌트 부착 시 전체 타입명**(`LoveAlgo.Game.GameManager`)으로 모호성 주의.
- **MCP 씬 작업 팁**(이번 세션 시행착오): 신 asmdef 타입을 MCP로 부착/배선 시 **assembly-qualified 이름** 필요 — `"LoveAlgo.UI.UIManager, LoveAlgo.UI"` 형식(단순명은 "not found"). 컴포넌트 ref 배선(`set_property`)은 **GO instanceID**를 주면 해당 GO에서 컴포넌트(TMP_Text·Button 등) 자동 추출. 프리팹화는 `manage_prefabs create_from_gameobject`(target=이름). **프로젝트는 Input System 패키지** — EventSystem엔 `InputSystemUIInputModule`(구 `StandaloneInputModule`은 런타임 `Input` 예외로 전 PlayMode 오염).
- **내러티브 씬 구조(Game.unity)**: 캔버스 1개(`_UI`, Screen Space) 유지. `_UI/Narrative`=UIManager.narrativeRoot(부팅 시 **inactive** — 시뮬 클릭 차단 방지, ShowUiGroupCommand가 토글). 그 아래 `DialogueView`(전체화면 투명 Image=클릭캐처+화자/본문 TMP)·`ChoiceView`(Container=VerticalLayoutGroup, slotPrefab=`Prefabs/Narrative/ChoiceSlot.prefab`). 매니저 `_Bootstrap/NarrativeController`·`FlowCommandController`(State=GameState_Main). dev 진입 = `_UI/Simulation/DevNarrativeButton`(NarrativeDevTrigger, 인라인 데모 CSV). **PlayMode 테스트 격리 주의**: GameScene 테스트가 Game.unity를 Single 로드 후 언로드 안 해 잔존 — 신 매니저를 발행 이벤트로 검증하는 테스트는 잔존 인스턴스 제거 후 진행(NarrativeControllerPlayModeTests 참고). **⚠️ 씬 dirty 오염**: PlayMode 테스트가 `Game.unity`를 SceneManager 로드하면 씬 오브젝트 active를 실제로 토글한다(EndingRoot 켜짐·UIManager가 Narrative 켜짐). 그 상태로 dirty 저장되면 **부팅 UI 상태가 디스크에 오염**된다 — 테스트 후 `Game.unity`를 저장하기 전 **부팅 active 상태 확인 필수**(Narrative=inactive·EndingRoot=inactive·Simulation=active).
- **~~IVT 가교~~ 제거 완료(`9152615`)**: 구 ScenarioEditor 삭제로 `Scripts/Narrative/AssemblyInfo.cs`(InternalsVisibleTo) 제거됨. 신 테스트는 Narrative internal 미사용(IVT 없이 EditMode 240 그린).
- **테스트 = Test Runner 어셈블리만**(임시 dev 하니스/씬 금지): EditMode=순수/공식층, PlayMode=MonoBehaviour 라이프사이클·OnEnable 구독·씬 와이어링. 구 코드 테스트 3개(ScriptParser·ScriptValidator·SaveLoadRoundTrip)는 `Assets/Tests/Editor/`(Assembly-CSharp-Editor) 잔류.
- **State SO 바인딩**: 매니저/컨트롤러는 `State` 프로퍼티(GameStateSO)를 인스펙터/부팅으로 주입받음. 통합 dev 씬이 그 배선 예시. 런타임 초기화(공식 Configure + DayLoop.BeginRun)는 `GameBoot.NewGame`/`GameBootstrap`.

---

## ✅ 이번 세션 검증·커밋 완료

- **타이틀 6버튼 메뉴 배선 — 2026-06-05 세션 1커밋(`b7f90c5`, EditMode 243·PlayMode 59→60 그린, 컴파일 0)**: 감독이 에디터에서 타이틀을 **6버튼(Start/Continue/Config/Load/Extra/Exit)+장식(로고·배경·꽃잎FX)**으로 재디자인(미커밋 WIP)했으나 코드가 3버튼만 알아 4버튼이 죽어있던 것을 정합. **TitleView 3→6버튼**(loadButton/extraButton/exitButton 추가), **Exit→`QuitGameCommand`(Core)→SceneFlowController 종료**(빌드 `Application.Quit`/에디터 PlayMode 정지, ADR-007 표시만·동작은 구독자), **Config/Load/Extra는 목적지 화면 미존재라 `Log.Info` 안내만**(과설계 게이트 #7 — 화면/커맨드 신설 없이 배선 구조만 준비). Title.unity 신규 3버튼 `_UI/TitleView` 바인딩(6개 GO 매핑 YAML 검증) + 폰트 2개 글리프 동적 베이킹(새 라벨/로고 수반, 순추가 +7/+33). PlayMode Exit→발행 테스트 +1. **남은 타이틀(각 별도 마일스톤, 현재 안내 로그로 작동)**: Config=설정(View·볼륨·AudioMixer)·Load=세이브 슬롯 화면·Extra=부가 콘텐츠.

- **타이틀 흐름 — 2026-06-05 세션 4커밋(EditMode 240→243·PlayMode 55→59 그린, 컴파일 0)**:
  - `a6a181e` **슬라이스1**: 이전 세션이 커밋 전 종료해 떠 있던 타이틀 흐름 검증·커밋. `StartNewGameCommand`(Core 의도) + `TitleView`(New Game→발행, ADR-007) + `SceneFlowController`(구독→`SceneManager.LoadScene("Game")`, 씬 자족·persistent 매니저 없음, ADR-013 씬축) + `Title.unity`(빌드 **index 0**, `_UI`=TitleView+버튼3 바인딩·`_Boot`=SceneFlowController·EventSystem=InputSystemUIInputModule) + PlayMode테스트. **Continue/Settings는 배치·바인딩만(리스너 미연결).**
  - `151940a` **BGM 에셋**: 데모 오디오 **10개**를 `Resources/Audio/BGM/`로 정식화(`title`+히로인별 roa/yeeun/daeun/heewon/bom+`daily1/2`·`night`·`whitenoise`). 원본 GUID가 BGM으로 승계·Demo는 새 GUID 재발급. 경로 로딩(`Resources.Load`)이라 **코드 무해**.
  - `ebca568` **타이틀 BGM**: `TitleView.Start`→`PlayBgmCommand("title")` 발행(titleBgm 인스펙터, 비우면 skip), Title 씬 `_Managers/AudioManager` 추가(구독·재생, AudioSource 자동생성·State 무관). 실제 음원 재생=감독 Play 확인 영역.
  - `66f0db0` **이어하기(Continue) 🔴**: `ContinueGameCommand`(Core) + `GameEntry`(BootMode{NewGame,Continue} **static 홀더** — 씬 로드는 인자 불가라 씬전환 1회성 의도를 static으로 전달, GameBootstrap이 `Consume`으로 읽고 NewGame 리셋. 감독 결정) + `SceneFlowController` 구독(모드 설정+씬로드) + `GameBoot.ContinueGame`(오토세이브 로드+공식 주입, **ResetRuntime/BeginRun 우회**, 실패 시 false→NewGame 폴백, `JsonSaveStore`+`gs.Load` 직접이라 asmdef 무변경) + `TitleView.continueButton`(오토세이브 없으면 `interactable=false`). 역직렬화는 기존 `GameStateSO.Load`/`SaveService.Load` 재사용이라 작업은 분기 배선뿐. EditMode +3·PlayMode +2.
  - **타이틀 흐름 일단락**(New Game·Continue·BGM 동작, 씬 진입점=Title.unity index0). 남은 버튼 **Settings는 별도 마일스톤**(View·볼륨·AudioMixer 완전 미구현). **다음 후보**: 실 트리거(이벤트→스크립트 매핑, dev 버튼 대체)·엔진 포맷 스토리 CSV·시뮬레이션 루프 심화·M3 내러티브 후속.

- **PhaseController(ADR-013, `c7d2b7f`) 검증 완료** — EditMode·PlayMode 그린. 단 PlayMode `GameSceneEnding`/`GameSceneSimulation`이 한때 "ScheduleView null"로 실패 → 원인은 **디스크 `Game.unity`의 `ScheduleUI`가 직전 PlayMode로 `m_IsActive:0` 오염 저장**(코드 무관). `git restore`+씬 재로드로 정본 복원 후 51/51 회복. 폰트 SDF `.asset` 3건도 동일 부류라 복원. **교훈 재확인: PlayMode 후 `Game.unity` 저장 금지**(부팅 active 오염) — 씬은 커밋 정본이 정확.

- **분기 읽기/쓰기 3슬라이스 A·B·C 커밋 완료(EditMode 245 · PlayMode 51 그린)** — 순수 `ConditionEvaluator`(구 EvaluateCondition 1:1, AND>OR 우선) 공유: (A) Flow `If:조건:점프대상`(참=점프/거짓=통과) + `NarrativeController.HandleFlow` If 분기 + `ConditionEvaluatorTests` 9. (B) 선택지 `if:조건` 필터 `ChoiceParser.VisibleOptions`(0개면 건너뜀) + `ChoiceParserTests` +2. (C) Flow `Flag:이름[:true|false]`·`Set` 별칭(gs.SetFlag, 무통지) + `FlowCommandInterpreterTests` +1. **런타임(점프/숨김/플래그)은 데모 CSV 미반영** — 다음 PlayMode/플레이 확인 시 `Flow,,If:...`·`Option ...|if:Flag:x`·`Flow,,Flag:x` 추가.

- **구 아키텍처 폐기 페이즈1 완료(`477042a`·`784d2a4`, 푸시됨)** — 감독 지시로 구 Assembly-CSharp(Service Locator+구 매니저/모듈) 전면 폐기 착수. **조사 확정**: 신 코드(`Scripts/`+asmdef)는 구 코드(`Services`·구 매니저·`Modules`·`Contracts`)를 **0 참조**(의존 구→신 단방향 — 구를 통째 삭제해도 신·테스트 안 깨짐). **유일 블로커였던** Game.unity의 구 UI 위젯 의존(스케줄 카테고리 탭 `ButtonEX`×5·`TabGroup`)을 신 `CategoryTab`/`CategoryTabBar`(LoveAlgo.Schedule, ScheduleSlot 패턴 · MCP 배열참조 한계로 자식 CategoryTab **자동수집**)로 **컴포넌트 스왑**(스프라이트·색·레이아웃 보존) → Game.unity 구 위젯 GUID **0**. 신 코드 0구독으로 죽어있던 카테고리 탭이 실동작(탭→`ShowCategory`). EditMode 245·PlayMode 55 그린. **다음=페이즈2**(다음 액션 #5).

**직전 세션 완료(푸시됨)**: vn_conventions 정본화(`c9272b9`) · FX 네이밍 ScreenFx→ScreenFade·CameraFx→Camera·`_ScreenOverlay`(`96a211a`) · 화면 페이즈 일원화 `ScreenPhase`+PhaseController(`c7d2b7f`) · 구 아키텍처 폐기 페이즈1(`477042a`·`784d2a4`).

**이번 세션 완료(2026-06-04)**: **구 아키텍처 폐기 페이즈2(`9152615`)** — 구 Assembly-CSharp 983파일/137,790줄 일괄삭제 + StatGauge 잔재 정리 + asmdef autoRef 복귀(상세=다음 액션 #5). 재작성이 신 아키텍처(EventBus+SO, asmdef 12개) **단일**로 수렴. **보류**: 씬 stale `m_EditorClassIdentifier` 2건(GUID 바인딩, 무해).

---

## ▶️ 다음 액션

이번 세션 **M3 슬라이스2 스테이지 BG+Char 첫 서브슬라이스 — 코드+테스트 완성**(커밋 b967218, EditMode 166/PlayMode 21 그린). 렌더링 구조 결정: **별도 `_Stage` 캔버스(UI Image, `_UI`보다 낮은 sortingOrder)** — 구 UI-Image 동작·동결 px 수치 재사용 + 캔버스 rebuild 격리(ADR-004), SpriteRenderer 카메라/정렬레이어 신설 회피(감독 결정). 감독이 다음 방향 택1:

1. **M3 슬라이스2 — 스테이지 씬 배선(즉시 후속)**: `Game.unity`에 `_Stage` 캔버스 + StageView 배선 + 데모 CSV BG/Char(HANDOFF 현재상태 末 참조, 🔴 씬 흐름). **그 다음 서브슬라이스**: FX(카메라/스크린)·Sound(BGM/SFX)·오토모드·인라인태그(`<emote>`/`<wait>`)·CG/SD/Overlay(로아 모드)·점프페이드·선택지 조건/이력·LockScreen 계열 Flow. **실 트리거**: dev 버튼(NarrativeDevTrigger)을 이벤트→스크립트 매핑으로 대체 — 스토리 데이터(엔진 포맷 CSV) 필요(현재 기획 CSV만). 별칭/카탈로그(한글명→ID, Default→코드)는 컨벤션 로딩에서 승격 필요.
2. ✅ **완료(`c7d2b7f`, enum=`ScreenPhase`)** — ~~화면 페이즈 상태머신~~(🔴, ADR-013) — Title↔Story↔Schedule↔Ending 전환을 단일 `PhaseController`로 일원화(현재 NarrativeController가 ShowUiGroupCommand 직접 토글). GamePhase enum(State SO) + 순수 PhaseService(FSM) + 의도 발행(RequestPhaseCommand). LockScreen은 Phase 아닌 Overlay(완료-핸들 복귀). 슬라이스2의 LockScreen/페이즈전환이 이 결정에 의존하므로 그 전에 구현 권장. 구현 시 확정: UIGroup↔GamePhase 매핑·씬 경계·Overlay 목록(ADR-013 末).
3. **시뮬레이션 루프 심화** — 카테고리 탭 UI 배선(현재 슬롯 동적생성만, 탭 버튼 미연결) / HUD·슬롯 시각 레이아웃 / 엔딩 결과 디테일(최고 호감도 등) / **Shop SessionBuff 복합효과 SO 데이터 보강**(코드 완성, ItemCatalog.asset에 SubEffect 부재 = 🟢 데이터). *(페이즈전환은 #2로 분리.)*
4. **Shop Gift 인벤토리(🔴 세이브 스키마)** — 선물 보관/소비. 단 소비처=내러티브 Event2/3라 M3 이후가 자연스럽다(지금은 죽은 코드).
5. ✅ **완료(`9152615`)** — ~~구 아키텍처 폐기 페이즈2(구 코드 일괄삭제)~~. 구 Assembly-CSharp **983파일/137,790줄 일괄삭제**(신 코드 0참조 asmdef 격리로 확정). 대상: `_Project/{Common,Contracts,Core,DevTools,Modules,UI}`·`Scenes/Main.unity`·`_Dev/Integration`·`Scripts/`(top, DebugPanel/DebugRemoteWindow/**WindowsBuildTool** 포함—감독 결정 삭제)·`Tests/Editor/SaveLoadRoundTrip`·Resources 구 SO 4종(AudioSettings·FXDefaultsConfig·LockScreen/·ResourceCatalog)·IVT. 사후: EditorBuildSettings Main제거(Game index0)·`LoveAlgo.UI`/`Save` asmdef autoRef→true·**구 StatGauge 게이지 5개 제거**(Game.unity HUD StatPanel 잔재, 페이즈1 누락 — 신 HudView 코드참조·갱신 0인 죽은 위젯, 스탯은 statText 텍스트 유지). 검증: 컴파일0·EditMode 240·PlayMode 55·missing0(`validate` clean)·구 `GamePhase` 소멸·asmdef없는 게임코드 0. **잔존(범위 밖)**: DOTween(서드파티)·InputSystem_Actions(Unity생성)는 asmdef 없으나 신 코드 미참조 추정 — 별도 정리 후보. StatGauge 게이지 시각이 필요하면 신 위젯으로 후속(🟢 HUD 레이아웃).

### 워크플로우 규율 (directive)
- **재설계 ≠ 전사(ADR-012 강화, 2026-06-02 감독 지적 · 2026-06-03 정본화)**: **정본 = `docs/vn_conventions.md` — 슬라이스 착수 시 구 코드 대신 이 문서를 본다**(렌더타깃 분류축·네이밍·CSV vs C# 경계·안티패턴·모범 예시). 요구사항(STORY_COMMANDS·REWRITE_FEATURE_INVENTORY §기능)에서 클래스 책임·데이터 흐름을 먼저 설계 — 구 코드 읽기 전에. 구 코드는 동작 의미·동결 수치·CSV 문법(ADR-009) 확인용으로만, 구조/네이밍/분해 답습 금지. 커밋 "이식(port)" 표현 금지. 레드플래그: "이 클래스/필드가 요구사항 때문인가, 구 코드가 있어서인가?"
- 무언가 만들 때마다 **작동 증거**: 순수/공식층=EditMode 테스트, MonoBehaviour 라이프사이클·구독·씬 와이어링=PlayMode 테스트. 임시 dev 하니스 금지 — Test Runner 어셈블리로. 위험도 등급 선언 + 커밋 "왜".
- **썸네일은 레이어 배제 캡처**가 필수 요구사항(옛 말썽: 안 잡혀야 할 UI 포함됨) — Save 썸네일 이식(M5) 시.

### ~~잔여 Common~~ 삭제 완료(`9152615`)
`ListenerBag.cs`·`SingletonMonoBehaviour.cs`·`Services.cs` 모두 페이즈2에서 구 코드와 함께 삭제됨.

### 마일스톤 (원안 — 실제론 감독이 슬라이스별 우선순위 지정)
M1 Core ✅ → M2 Data/공식 ✅ → M3 내러티브/스테이지(파서까지) → M4 기능모듈(Schedule·Shop 진행) → M5 UI/Save(매니저·HUD 진행). *순서는 엄격히 안 지켜졌고(감독 선택), broad-first로 골격을 먼저 세웠다 — 다음은 depth(플레이 루프/내러티브 런타임)로 좁히는 게 자연스럽다.*

---

*결론과 가드레일만 전달. 상세 규칙 = docs/dev_guide.md, 기능 = docs/REWRITE_FEATURE_INVENTORY.md, 슬라이스 이력 = git log. 막히면 감독에게 질문.*
