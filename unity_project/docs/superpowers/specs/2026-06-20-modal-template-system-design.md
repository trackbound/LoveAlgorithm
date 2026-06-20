# 모달 템플릿 시스템 — 설계 스펙

- 날짜: 2026-06-20
- 위험도: 🟠 High (ModalView 리팩터 + Modal 프리팹 재구성)
- 상태: 설계 승인 완료 → 구현 계획(writing-plans)

---

## 1. 배경 & 문제

`ModalView`는 단일 제네릭 모달이다. `ShowModalCommand`(제목·본문·버튼 리스트)를 구독해, 버튼을 종류(`ModalButtonKind`)별 스타일 프리팹으로 `Buttons`(HorizontalLayoutGroup) 컨테이너에 **동적 스폰**한다.

현재 `Modal.prefab` 구조:
```
Modal (ModalView + Canvas + GraphicRaycaster)
└─ Root
   ├─ Dim (Image, 배경 차단)
   ├─ Panel (Image)
   ├─ Title (TMP)
   ├─ Message (TMP)
   └─ Buttons (HorizontalLayoutGroup) ← 버튼 동적 스폰
```

문제: 버튼 배치를 HorizontalLayoutGroup이 자동 정렬하므로 **메시지·버튼의 정교한 배치·간격·폰트를 모양별로 픽셀 단위로 제어하기 어렵다**. 감독은 자주 쓰는 모달 모양(yes/no, yes-only)을 **에디터에서 한 번 정교하게 배치해 고정 "틀"로 유지**하고 싶다.

실제 사용 중인 모양(소비처 전수):
- **2버튼 `[No, Yes]`**: TitleView 종료 확인, QuickMenuView 종료, SaveLoadView 덮어쓰기/새 저장
- **1버튼 `[Yes]`**: TitleView 이어하기 없음 안내
- **1버튼 `[Close]`**: SaveLoadView 오토세이브 안내, ShopView 알림(파킹)
- 테스트: 종류 미지정(`ModalButtonKind.Default`) 2버튼

## 2. 목표 & 비목표

**목표**
- 자주 쓰는 모달 모양을 **버튼까지 박힌 정교한 템플릿 프리팹**으로 제작·고정.
- `yes/no`·`yes-only` 두 틀을 정교하게 만든다(메시지 위치·폰트·버튼 배치 픽셀 단위).
- `ShowModalCommand` 제네릭 계약을 보존해 **모든 소비처 호출부 무변경**.
- 템플릿이 없는 모양은 **기존 동적 레이아웃으로 폴백**(SaveLoad/Shop 등 무중단).

**비목표 (후속/유지)**
- Close 1버튼 등 나머지 모양의 전용 틀 제작(원하면 후속 — 폴백이 흡수).
- `ShowModalCommand`/`ModalButton`/`ModalButtonKind` API 변경(불변).

## 3. 아키텍처

```
Modal.prefab (셸)
└─ Root
   ├─ Dim                 ← 공통(배경 차단)만 셸에 유지
   └─ TemplateContainer   ← 선택된 템플릿이 인스턴스화되는 빈 컨테이너(stretch)
```

모든 "내용"(패널·타이틀·메시지·버튼)은 **템플릿 프리팹**으로 분리. 셸엔 Dim + TemplateContainer만.

**템플릿 3종**
- `ModalYesNo.prefab` — 시그니처 `[No, Yes]`. No(좌)·Yes(우) 버튼이 박힌 정교한 틀.
- `ModalYesOnly.prefab` — 시그니처 `[Yes]`. Yes(가운데) 버튼이 박힌 정교한 틀.
- `ModalDynamic.prefab` — 시그니처 빈 배열(= 폴백). 현재 `Panel/Title/Message/Buttons(HLG)`를 그대로 추출. 종류별 버튼을 동적 스폰.

## 4. `ModalTemplate` 컴포넌트

템플릿 프리팹 루트에 부착. ModalView가 읽는 "틀의 약속".

```csharp
namespace LoveAlgo.UI
{
    public class ModalTemplate : MonoBehaviour
    {
        [Tooltip("이 틀이 담당하는 버튼 종류 순서(예: [No,Yes], [Yes]). 빈 배열 = 동적 폴백 틀.")]
        public ModalButtonKind[] signature;
        [Tooltip("제목 TMP(선택, 미바인딩 시 제목 생략).")]
        public TMPro.TMP_Text title;
        [Tooltip("본문 TMP(선택).")]
        public TMPro.TMP_Text message;
        [Tooltip("정적 틀: 미리 배치된 버튼 슬롯(좌→우). 동적 폴백이면 비움.")]
        public ChoiceSlot[] slots;
        [Tooltip("동적 폴백 전용: 종류별 버튼을 스폰할 컨테이너. 정적 틀이면 비움.")]
        public Transform dynamicContainer;

        /// <summary>정적 틀이면 true(slots 사용), 폴백이면 false(dynamicContainer 사용).</summary>
        public bool IsStatic => slots != null && slots.Length > 0;
    }
}
```

## 5. ModalView 선택·바인딩 로직

ModalView는 **템플릿 프리팹 리스트**(`[SerializeField] List<ModalTemplate> templates`)를 직렬화한다(각 원소 = `ModalYesNo`/`ModalYesOnly`/`ModalDynamic` 프리팹). 정확히 하나의 원소만 `signature`가 빈 배열 = 폴백.

`OnShow(ShowModalCommand e)`:
1. **시그니처 매칭**: `e.Buttons`의 `Kind` 순서열과 각 템플릿의 `signature`를 비교(순서·길이 일치 = 매칭). 매칭 없으면 빈 시그니처(폴백) 템플릿 선택.
2. 선택된 템플릿 프리팹을 `TemplateContainer`에 `Instantiate`. `Root.SetActive(true)`.
3. 템플릿의 `title`/`message`에 `e.Title`/`e.Message` 세팅(미바인딩 시 생략).
4. **정적 틀**(`IsStatic`): `slots[i].Bind(i, e.Buttons[i].Label, OnSelected)` — 버튼 스킨은 이미 박혀 있어 라벨·콜백만. (`slots.Length`와 `e.Buttons.Count` 불일치 시 `Debug.LogError` 후 가능한 만큼 바인딩.)
   **폴백**: 기존 로직대로 `PrefabFor(kind)`를 `dynamicContainer`에 `Instantiate` + `Bind`.
5. Esc 취소(마지막 버튼)·단일 모달(새 명령 시 기존 닫고 재생성)·핸들 회수(`Close`)는 **기존 동작 보존**. 닫을 때 인스턴스화된 템플릿을 `Destroy`.

**순수 결정층(EditMode 테스트 대상)**:
```csharp
// 명령의 버튼 종류 순서열 → 선택할 템플릿 인덱스. 정확 매칭 우선, 없으면 빈 시그니처(폴백) 인덱스,
// 폴백도 없으면 -1. GameObject 불필요.
static int MatchTemplate(IReadOnlyList<ModalButtonKind> commandKinds, IReadOnlyList<ModalButtonKind[]> signatures);
```

## 6. 이행

1. `Modal.prefab`에서 `Panel/Title/Message/Buttons`를 **`ModalDynamic.prefab`로 추출**(ModalTemplate 부착: signature=빈, message/title/dynamicContainer 배선). 셸엔 Dim + TemplateContainer만 남김.
2. `ModalYesNo.prefab`/`ModalYesOnly.prefab` 신규 제작: 패널·메시지·버튼(YesButton/NoButton 인스턴스)을 정교 배치, ModalTemplate 배선(signature·title·message·slots).
3. `ModalView` 리팩터: 템플릿 리스트(`ModalTemplate[]` 또는 프리팹 리스트) 직렬화 + 선택/바인딩. 기존 동적 스폰 로직은 폴백(ModalDynamic) 경로로 보존.
4. 소비처(TitleView/QuickMenu/SaveLoad/KeyReset/Shop/Schedule) **무변경** — `[No,Yes]`/`[Yes]`만 정교한 틀로 자동 라우팅.

## 7. 테스트

- **EditMode** (`ModalViewMatchTests`): `MatchTemplate` 순수 함수 — `[No,Yes]`→YesNo 인덱스, `[Yes]`→YesOnly 인덱스, `[Default,Default]`/`[Close]`/`[]`→폴백(빈 시그니처) 인덱스, 순서 다르면 정확매칭 실패→폴백(`[Yes,No]`≠`[No,Yes]`), 폴백 없는 리스트면 -1.
- **PlayMode** (`ModalTemplatePlayModeTests`): 정적 틀 경로 — `[No,Yes]` 명령 → YesNo 템플릿 인스턴스화, 슬롯 2개에 라벨 바인딩, `slots[1]` 클릭→인덱스 1 회수. `[Yes]` → YesOnly, 1슬롯.
- **회귀**: 기존 `ModalViewPlayModeTests`(종류 `Default` → 폴백 경로)와 `GameSceneOverlayBootPlayModeTests`(`[Close]` → 폴백)는 **그대로 통과**해야 한다(폴백이 현 동작 보존).

## 8. 위험 & 완화

- 🟠 Modal.prefab 재구성 + ModalView 리팩터 → Git diff 검토. 모든 소비처 모양(2버튼·1버튼·Default·Close)을 PlayMode로 커버.
- 시그니처 매칭은 **종류 순서**에 의존 — 소비처가 No-좌/Yes-우 관례를 지키는지 확인(현재 전부 준수). 어긋나면 미매칭→폴백이라 안전(깨지지 않고 동적 표시).
- 템플릿 슬롯 수 ≠ 명령 버튼 수 엣지: 로그 + 가능한 만큼 바인딩(정적 틀은 시그니처로 이미 개수 일치 보장).

## 9. 향후 (이 스펙 밖)

- Close 1버튼 등 추가 모양의 전용 틀 승격(원하는 만큼, 폴백에서 이전).
- 템플릿 선택을 종류 시그니처 대신 명시적 키로 전환(필요 시).
