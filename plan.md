Context
데모 빌드 QA 후 팀원들이 보고한 버그/개선 목록. 개발(재환) 담당 코드 수정 항목만 정리.
시나리오PM/UI디자이너 담당(CSV 대사 수정, 배경 교체, 리소스 전달 등)은 제외.

이미 반영된 항목 (변경 불필요)

 BGM 무한반복 재생 (bgmSource.loop = true - AudioManager.cs:76)
 선택지 순차 등장 애니메이션 (ChoiceUI.cs:61-75, staggered 0.1s delay)
 Auto 모드 텍스트 길이 반영 (ScriptRunner.cs:473-476, dynamic delay)
 타이핑 문장부호 딜레이 (DialogueUI.cs:164-172, .!?~… 추가 딜레이)
 CamShake 프리셋 Weak/Medium/Strong (ScreenFX.cs:44-47, 126-146)
 인디케이터 바운스+깜빡임 (DialogueUI.cs:538-647)
 인라인 emote 태그 (DialogueUI.cs:186-189)
 EyeOpen/Close/Blink 효과 (ScreenFX.cs:154-509)
 독백 dots 애니메이션 (DialogueUI.cs:54-426)
 CG 표시 시 캐릭터 퇴장 + 대사창 숨김 (ScriptRunner.cs:550-585)


수정 항목
Sprint 1: 게임 진행 불가 버그 (Critical)
1. 타이틀 복귀 시 BGM 미출력

파일: Assets/Scripts/Core/GameManager.cs
원인: ChangePhase()에서 BGM 정지가 타이틀에서 나갈 때만 실행됨. 타이틀로 돌아올 때는 기존 BGM이 계속 재생되어 Title BGM이 안 나옴
수정: ChangePhase() 메서드에 타이틀 진입 시 BGM 정지 조건 추가

csharp// 기존 코드 (line 62-66 근처):
if (prevPhase == GamePhase.Title && newPhase != GamePhase.Title)
{
    AudioManager.Instance?.StopBGMAsync().Forget();
}
// 아래 추가:
else if (newPhase == GamePhase.Title && prevPhase != GamePhase.Title)
{
    AudioManager.Instance?.StopBGMAsync().Forget();
}
2. Continue 버튼 작동 안 됨

파일: Assets/Scripts/Core/GameManager.cs
원인: 1번 문제 해결 후 재확인 필요. LoadFromSaveData() 비동기 흐름에서 BGM Stop이 .Forget()으로 fire-and-forget 처리되어 타이밍 이슈 가능
수정: LoadFromSaveData()에서 BGM Stop을 await로 변경 + 디버그 로그 추가

csharp// line 302 변경:
await Story.AudioManager.Instance?.StopBGMAsync();  // .Forget() 제거
// 디버그 로그 추가:
Debug.Log($"[GameManager] LoadFromSaveData: Phase={data.Phase}, Script={data.ScriptName}");
3. 로드 시 처음부터 시작됨

파일: Assets/Scripts/Core/GameManager.cs
원인: LoadFromSaveData()의 Phase 체크 조건이 Prologue/DayLoop만 허용. 다른 Phase면 스크립트 복원 없이 Phase만 변경
수정: Phase 조건 완화 및 디버그 로그로 실제 데이터 확인

csharp// line 316-317: 조건 확장 (Ending 등 추가)
if (!string.IsNullOrEmpty(data.ScriptName))
{
    // Phase 무관하게 스크립트 위치가 있으면 복원 시도
    ...
}
4. 플레이 중 설정 변경 안 됨 (TextSpeed/AutoSpeed)

파일들:

Assets/Scripts/Story/DialogueUI.cs - SetTextSpeed() 메서드 추가
Assets/Scripts/Story/ScriptRunner.cs - SetAutoDelay() 메서드 추가
Assets/Scripts/UI/SettingsPopup.cs - TODO 주석을 실제 호출로 교체


수정 내용:

DialogueUI.cs에 추가:
csharppublic void SetTextSpeed(float normalized)
{
    // 0=느림(0.08s/char), 1=빠름(0.01s/char)
    typingSpeed = Mathf.Lerp(0.08f, 0.01f, normalized);
}
ScriptRunner.cs에 추가:
csharppublic void SetAutoDelay(float normalized)
{
    // 0=느림(4초), 1=빠름(0.5초)
    autoDelayBase = Mathf.Lerp(4.0f, 0.5f, normalized);
}
SettingsPopup.cs line 317-328 수정:
csharpvoid OnTextSpeedChanged(float value)
{
    var runner = ScriptRunner.Instance;
    var dialogueUI = UIManager.Instance?.DialogueUI;
    dialogueUI?.SetTextSpeed(value);
}

void OnAutoSpeedChanged(float value)
{
    ScriptRunner.Instance?.SetAutoDelay(value);
}
Awake 시 PlayerPrefs에서 저장된 값 로드하는 로직도 DialogueUI.Awake, ScriptRunner.Start에 추가.

Sprint 2: BGM/사운드
5. 모든 음악 시작/끝 3초 페이드

파일: Assets/Scripts/Story/AudioManager.cs
수정: defaultFadeDuration 값 1.5f → 3.0f로 변경


Sprint 3: 이름 설정
6. 기본 이름 '도윤' (빈 엔터 시)

파일: Assets/Scripts/UI/UsernameUI.cs
수정:

csharp[SerializeField] string defaultName = "도윤";

void OnConfirmClick()
{
    string name = inputField?.text?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(name))
        name = defaultName;

    var result = NameValidator.Validate(name);
    // ... 기존 로직
}

void OnInputSubmit(string value)
{
    OnConfirmClick();  // 빈 입력도 기본 이름으로 처리
}
7. 욕설 방지 (금지어 목록 확장)

파일: Assets/Scripts/Core/NameValidator.cs
수정: 하드코딩 배열 → Resources/Data/BannedWords.txt에서 로드

csharpstatic string[] _bannedWords;
static string[] BannedWords => _bannedWords ??= LoadBannedWords();

static string[] LoadBannedWords()
{
    var asset = Resources.Load<TextAsset>("Data/BannedWords");
    if (asset != null)
    {
        return asset.text
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => !string.IsNullOrEmpty(w))
            .ToArray();
    }
    return new[] { "admin", "관리자", "운영자", "gm" };
}

추가 파일: Assets/Resources/Data/BannedWords.txt 생성 (Google Sheets 금지어 목록 복사)


Sprint 4: 게임 흐름
8. 날 바뀔 때 5초 블랙 페이드

파일: Assets/Scripts/Core/GameManager.cs
수정: EndDay()를 async로 변환

csharpasync void EndDay()
{
    await ScreenFX.Instance.FadeOutAsync(2.5f);
    CurrentDay++;
    RemainingActions = 3;
    AutoSave();
    await UniTask.Delay(500);
    ChangePhase(GamePhase.DayLoop);
    await ScreenFX.Instance.FadeInAsync(2.0f);
}
9. 분량 끝 → 저장 팝업 → 타이틀 복귀

파일: Assets/Scripts/Core/GameManager.cs
수정: OnScriptEnd 핸들러에 종료 시퀀스 추가

csharpasync UniTaskVoid ShowEndOfContentAsync()
{
    await ScreenFX.Instance.FadeOutAsync(2f);
    bool save = await PopupManager.Instance.ConfirmAsync("저장하시겠습니까?");
    if (save) AutoSave();
    GoToTitle();
}

Sprint 5: 캐릭터/배경 타이밍
10. 공간 전환 시 2초 페이드

파일: Assets/Scripts/Story/BackgroundLayer.cs
수정: Fade 기본 duration 0.5f → 2.0f

11. 캐릭터 등장 시 1초 페이드

파일: Assets/Scripts/Story/CharacterSlot.cs
수정: fadeDuration 0.3f → 1.0f

12. 대사 줄바꿈 시 "/" 표시

파일: Assets/Scripts/Story/ScriptParser.cs
수정: CSV 확인 후, 줄바꿈 마커 /를 \n으로 치환하는 로직 추가 (CSV 내용 확인 필요)


Sprint 6: 세이브 슬롯 스크린샷
13. 세이브 슬롯에 이미지 표시

파일들:

Assets/Scripts/UI/SaveLoadSlot.cs - Image screenshotImage 필드 추가
Assets/Scripts/Story/SaveManager.cs - 스크린샷 캡처/저장/로드
Assets/Scripts/Core/GameManager.cs - Save() 호출 시 스크린샷 포함


구현 방식:

Save 시 ScreenCapture.CaptureScreenshotAsTexture() 호출
Texture2D → PNG byte[] → 별도 파일 저장 (save_{slot}_thumb.png)
Load 시 PNG 파일 읽어서 Texture2D → Sprite로 변환 후 표시
SaveLoadSlot.SetData()에 screenshotImage.sprite 설정 추가




Sprint 7: UI 수정
14. 선택지 기본 색상 수정 (이미 수정됨 - 바이브코딩으로 prefab 수정 완료)
15. 선택지 호버 후 글씨 색 안 돌아오는 문제

파일: Assets/Scripts/Story/ChoiceUI.cs
수정: 버튼 생성 시 Button.transition = Selectable.Transition.None 설정하고, HoverColorSwap 컴포넌트를 동적으로 추가하여 정확한 색상 복원 보장

16. 엑스트라 메뉴 클릭 시 창 안 뜸

작업: Inspector에서 PopupManager.modalPrefabs에 ExtraPopup 프리팹 추가 (코드 변경 없음)

17. 대사창 "펼치기" 텍스트 없음

작업: Prefab에서 showButtonObject에 TMP_Text 자식 추가, 텍스트 "펼치기"

18. 저장 완료 팝업 양 끝 어두움

작업: Toast 팝업 prefab 배경 이미지의 스프라이트/머터리얼 수정

19. 저장 확인 버튼 초기 회색

작업: ConfirmPopup prefab의 Button ColorBlock normalColor 확인 및 수정


Sprint 8: 폰트 적용

작업: TMP Font Asset 생성 후 각 prefab에 Inspector에서 적용
폰트 매핑:

설정창 제목: 210수퍼사이즈
카테고리/항목/버튼: 어그로 Medium
설명: 어그로 Light
대사: Pretendard SemiBold
이름/선택지/팝업: 어그로 Light
로그: 이름=어그로 Medium, 대사=Pretendard Medium
독백: 어그로 Light + 그림자(#ff669d)
이름 입력: Pretendard Medium




CSV/시나리오 팀 전달 (코드 아님)

 다은 BGM 종료 시점에 Sound,,BGM:Stop,> 추가
 Night BGM 재생 명령 추가
 잠자기 후 FadeIn 누락 확인
 예은 캐릭터 Enter/Exit 순서 정리
 희원/봄 Speaker를 성+이름으로 수정
 독백 시 Overlay dim 명령 추가
 로아 배경 교체 (0207 업로드 버전)
 볌써 → 벌써 오타 수정

Prefab/Inspector 작업 (코드 아님)

 ExtraPopup 프리팹 → PopupManager.modalPrefabs에 추가
 Toast 팝업 배경 이미지 수정
 ConfirmPopup 버튼 초기 색상 수정
 Show 버튼에 "펼치기" 텍스트 추가
 CharacterDatabase에 하예은(Yeun) 데이터 확인
 TMP Font Asset 생성 및 적용
 캐릭터 이미지 Import Settings 확인 (깨짐 문제) - 파이썬 스크립트 아님, Unity Texture Import Settings에서 Filter Mode=Bilinear, Compression=High Quality, Max Size를 원본 해상도 이상으로 설정 필요. 특히 봄/다은 캐릭터
 설정창 fullscreen/windowed 버튼 HoverColorSwap 추가

검증 방법

Play → 프롤로그 진행 → 타이틀 복귀 → BGM 확인 → Continue 클릭 → 이어서 진행 확인
Save → Load → 세이브 지점에서 재개 확인 + 슬롯 스크린샷 표시 확인
설정 → 볼륨/속도 변경 → 인게임 즉시 반영 확인
이름 입력 → 빈 엔터 → "도윤" 확인 / 욕설 → 차단 확인
선택지 → 호버 → 마우스 이탈 → 색상 복원 확인
BGM 전환 시 3초 페이드 확인
날 바뀔 때 블랙 페이드 5초 확인