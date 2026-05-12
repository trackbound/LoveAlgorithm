# MIGRATION_PLAN.md

> 래퍼 → 완전 이주 마스터 플랜. 토큰 효율과 안전성 모두 고려.

---

## 목표 폴더 구조 (재확정)

```
Assets/_Project/
├── Core/                  글로벌 인프라
├── Common/                EventBus, Services, 베이스 클래스
├── Modules/{Name}/        자기완결 모듈
│   ├── Code/              .cs (네임스페이스: LoveAlgo.Modules.{Name})
│   ├── Data/              SO (필요 시 Resources/Data/ 동봉)
│   ├── Prefabs/           프리팹
│   └── Art/               모듈 전용 아트
└── Scenes/                씬

Assets/Art/               공용 아트만
Assets/ThirdParty/        DOTween, Feel 등
Assets/Resources/         거의 비움 (Story CSV 등 Resources.Load 필수만)
```

**근거:** 모듈 자기완결 = 삭제 1폴더, 이해 1폴더, AI 컨텍스트 1폴더.

---

## 모델·세션 분리 전략

| 단계 | 모델 | 비용 추정 | 책임 |
|------|------|----------|------|
| 0. 정리 (수동) | — | 0 | 사용자: 빈/죽은 폴더 삭제 |
| 1. 마스터 플랜 | Opus | 5-10턴 | **이 문서 + 모범 1개** |
| 2. Audio 모범 이주 | Opus | 20-30턴 | 레시피 검증, 패턴 확정 |
| 3. 모듈 레시피 작성 | Opus | 5턴 × N | 모듈별 상세 스펙 |
| 4. 모듈 실행 | Sonnet sub-agent | 20-40턴/모듈 | 기계적 이동·갱신 |
| 5. 단순 탐색 | Haiku sub-agent | 5-10턴/회 | 호출자 목록, 파일 존재 확인 |
| 6. 검증 | 사용자 | 0 | Unity 컴파일 확인 |
| 7. 통합 리뷰 | Opus | 5턴/모듈 | sub-agent 보고 검토 |

**예상 절감:** Opus 단독 $50-200 → 분리 전략 $7-20.

---

## 이주 순서·의존성

```
독립 (병렬 가능): Audio, LockScreen(신규), Gacha(신규)
계산기 분리:      Affinity
얇은 래퍼 정착:   Stats (이미 🟦, 확인만)
복잡 분리:        DayLoop, Save, Schedule, Shop, Inventory, Phone, MiniGame
대형 분리:        Narrative (마지막)
```

### 우선순위 (예상 시간 short → long)

| # | 모듈 | 위험도 | 우선 사유 |
|---|------|--------|----------|
| 1 | **Audio** | 낮 | 자기완결, 모범 |
| 2 | **Affinity** | 낮 | 명확한 API, 호출자 적음 |
| 3 | **Stats** (확정) | 매우 낮 | 확인만, 파일 이동 없음 |
| 4 | **Schedule** | 중 | Schedule UI/효과 분리 |
| 5 | **Shop** + **Inventory** 분리 | 중 | 같은 작업에 묶어 |
| 6 | **Phone** | 중 | 메신저, 미완성 프리팹 정리 함께 |
| 7 | **MiniGame** | 중 | 단순 |
| 8 | **Save** | 중-높 | 직렬화 호환성 주의 |
| 9 | **DayLoop** | 높 | GameManager와 얽힘 |
| 10 | **Narrative** | 매우 높 | 가장 큰 변경. 마지막 |

신규 모듈 (LockScreen, Gacha)은 기능 작업 시 처음부터 새 구조.

---

## 표준 레시피 템플릿

각 모듈은 `docs/migrations/{Module}.md` 1장. 양식:

```markdown
# {Module} Migration Recipe

## 출처 (Source)
- `Assets/Scripts/{기존경로}/A.cs`
- `Assets/Scripts/{기존경로}/B.cs`

## 목적지 (Destination)
- `Assets/_Project/Modules/{Module}/Code/A.cs`
- `Assets/_Project/Modules/{Module}/Code/B.cs`

## 네임스페이스
- old: `LoveAlgo.X`
- new: `LoveAlgo.Modules.{Module}`

## 공개 표면 (IService)
- IModule 인터페이스 (이미 있음 / 신규)
- 외부 호출 가능한 메서드 목록

## 호출자 (검색 결과)
1. `Assets/Scripts/Y/Z.cs` — 호출: A.Foo()
2. ...

## 갱신 작업
- [ ] 파일 이동 (.cs + .meta)
- [ ] 네임스페이스 변경 in moved files
- [ ] 호출자 `using` 추가/갱신
- [ ] 검증: Unity 컴파일

## 위험·주의
- (예: Resources.Load 경로, 싱글톤 초기화 순서, ...)

## 사용자 수동 확인
- 컴파일 OK
- (옵션) 런타임 검증
```

---

## 자동화 규칙 (sub-agent 지침)

각 sub-agent에 전달할 공통 규칙:

1. **파일 이동 시 .meta 함께 이동** (Unity 참조 보존)
2. **네임스페이스 변경은 한 번에 변경, 부분 변경 금지**
3. **호출자 갱신은 Serena `find_referencing_symbols` 결과 기반** — Grep 추측 금지
4. **모호하면 멈추고 보고** — 추측 금지
5. **컴파일 검증은 사용자 책임** — Claude는 시도하지 않음
6. **보고 형식**: 이동 파일 N개 / 갱신 호출자 N개 / 미해결 의문 N개
7. **권한 위반 (Bash deny 패턴 등) 발견 시 즉시 멈춤**

---

## 정리 (사용자 수동, 무비용)

이주 시작 전 또는 병행. Unity 에디터에서 삭제 권장 (Project 뷰 → Delete):

| 대상 | 사유 | 조치 |
|------|------|------|
| `Assets/Character Origin/` | 빈 폴더 | 삭제 |
| `Assets/_Recovery/0.unity*` | 정체 불명 잔재 | 백업 확인 후 삭제 |
| `Assets/Art/_Sample/*.png` | 목업 이미지 (참고용?) | 정말 참고용이면 `docs/refs/`로 이동, 아니면 삭제 |
| `Assets/Prefabs/_TODOs/Phone/*` | 미완성 Phone 프리팹 | Phone 모듈 이주 시 모듈 폴더로 흡수 |
| `_ThirdParty/TextMeshPro` | Unity 빌트인과 중복? | 확인 후 정리 |

`Fonts/Origin/` — 정상 자산일 수 있음 (사용자 판단).

---

## 진행 룰

1. 모듈 1개씩 (병렬 sub-agent로 묶을 땐 의존성 없는 것만)
2. 각 모듈 끝나면 **사용자가 Unity 컴파일 확인 후 다음으로**
3. sub-agent 보고에 의문점 있으면 Opus가 해결
4. 이주 완료된 모듈은 `MODULE_STRUCTURE.md` 표 상태 ✅ 갱신
5. `WORK_PLAN.md`에 완료 항목 누적

---

## 다음 행동

1. **이 문서 검토** (사용자)
2. **Audio 레시피 작성 + 직접 수행** (Opus, 본 세션)
3. **다음 모듈부터 sub-agent 위임**
