# LoveAlgo Asset Naming Convention

## 목적
AI가 에셋을 직관적으로 파악하고, 일관된 패턴으로 코드를 생성할 수 있도록 명명 규칙을 표준화합니다.

## 핵심 원칙
1. **영문 사용** - 파일명, 폴더명 모두 영문 (한글 금지)
2. **PascalCase 우선** - 캐릭터명, 표현명, 기능명
3. **언더스코어 구분** - 복합 이름은 `_`로 구분
4. **접두어 일관성** - 타입별 접두어 필수

---

## 1. 캐릭터 스프라이트

### 구조
```
Assets/Resources/Characters/{CharacterName}/{Expression}.png
```

### 규칙
| 요소 | 규칙 | 예시 |
|------|------|------|
| 캐릭터명 | PascalCase 영문 | `Bom`, `Daeun`, `Heewon`, `Roa`, `Yeun` |
| 표현명 | PascalCase | `Default`, `Happy`, `Surprise`, `Tearful` |
| 파일 확장자 | `.png` | `Default.png` |

### 표준 표현 목록
| 표현 | 용도 |
|------|------|
| `Default` | 기본 중립 표정 |
| `BrightSmile` | 밝은 미소 |
| `EyeSmile` | 눈웃음 |
| `Happy` | 기쁨/환희 |
| `Surprise` | 놀람 |
| `Glare` | 노려보기/화남 |
| `Tearful` | 눈물/슬픔 |

### CSV에서 참조
```csv
Char,,Bom:Happy,>
```

---

## 2. 배경 (Background)

### 구조
```
Assets/Resources/Backgrounds/BG_{Location}_{Detail}_{Time}.png
```

### 규칙
| 요소 | 규칙 | 예시 |
|------|------|------|
| 접두어 | `BG_` | 필수 |
| 장소 | PascalCase 영문 | `Campus`, `Engineering`, `MyRoom` |
| 세부 | PascalCase (선택) | `Street1`, `Classroom`, `Interior` |
| 시간 | `Day` / `Night` (선택) | `Day`, `Night` |
| 파일 확장자 | `.png` | |

### 예시
```
BG_Campus_Street1_Day.png
BG_Engineering_Classroom.png
BG_MyRoom_Interior_Night.png
BG_BlackCut.png
```

### CSV에서 참조
```csv
BG,,BG_Campus_Street1_Day,>
```

---

## 3. CG (Cut Graphic)

### 구조
```
Assets/Resources/CG/CG_{Character}_{Number}.png
```

### 규칙
| 요소 | 규칙 | 예시 |
|------|------|------|
| 접두어 | `CG_` | 필수 |
| 캐릭터명 | PascalCase | `Roa`, `Yeun`, `Bom` |
| 번호 | 2자리 숫자 | `01`, `02`, `03` |

### 예시
```
CG_Roa_01.png
CG_Yeun_01.png
```

### CSV에서 참조
```csv
CG,,CG_Roa_01,>
```

---

## 4. SD (Story Cutscene)

### 구조
```
Assets/Resources/SD/SD_{Character}_{Number}.png
```

### 규칙
| 요소 | 규칙 | 예시 |
|------|------|------|
| 접두어 | `SD_` | 필수 |
| 캐릭터명 | PascalCase | `Bom`, `Daeun` |
| 번호 | 2자리 숫자 | `01`, `02` |

### 예시
```
SD_Bom_01.png
SD_Daeun_01.png
```

---

## 5. BGM

### 구조
```
Assets/Resources/Audio/BGM/{Name}.mp3
```

### 규칙
| 유형 | 규칙 | 예시 |
|------|------|------|
| 캐릭터 테마 | 캐릭터명 | `Bom.mp3`, `Roa.mp3` |
| 일상/배경 | 분위기 + 번호 | `Daily1.mp3`, `Night.mp3` |
| 시스템 | 기능명 | `Title.mp3` |

### CSV에서 참조
```csv
Sound,,BGM:Bom,>
```

---

## 6. SFX

### 구조
```
Assets/Resources/Audio/SFX/{ID}_{Name}.{ext}
```

### 규칙
| 요소 | 규칙 | 예시 |
|------|------|------|
| ID | 3자리 숫자 | `001`, `060`, `119` |
| 이름 | PascalCase | `Pop`, `Message`, `Blessing` |
| 확장자 | `.wav`, `.mp3`, `.ogg` | 혼합 허용 |

### 예시
```
001_Pop.wav
060_Message.mp3
119_Blessing.mp3
UI_Click.ogg
```

### 특수 케이스 (ID 없음)
```
Click.wav
Hover.wav
vn_type.wav
UI_Click.ogg
UI_Hover.ogg
```

---

## 7. 아이템

### 구조
```
Assets/Art/Item/{name}.png
Assets/Art/Item/Icon/{name}.png
```

### 규칙
| 요소 | 규칙 | 예시 |
|------|------|------|
| 이름 | 영문 소문자 + `_` | `energybar`, `rabbit_keyring` |
| 공백 | `_` 또는 공백 허용 | `marron bread.png`, `ankle support.png` |
| 아이콘 | `Icon/` 서브폴더 | `Icon/energybar.png` |

### 권장 개선
```
기존: marron bread.png
권장: marron_bread.png
```

---

## 8. UI 아트

### 구조
```
Assets/Art/UI/{Category}/{SubCategory}/
Assets/Resources/UI/{Category}/
```

### 규칙
| 카테고리 | 폴더 | 용도 |
|----------|------|------|
| 확정 아트 | `Done/Common/`, `Done/Dialogue/` | 최종 UI 리소스 |
| 폰/메신저 | `Phone/ChatBubble/`, `Phone/ChatRoom/` | 메신저 UI |
| 로딩 | `Resources/UI/Loading/` | 로딩 화면 |
| 목업 | `Sample/` | 참조용 (비확정) |

### 로딩 화면 규칙
```
Loading_{Character}_{N}.png
예: Loading_Bom_01.png, Loading_Roa_02.png
```

---

## 9. 프리팹

### 구조
```
Assets/Prefabs/{Category}/{Name}.prefab
```

### 규칙
| 요소 | 규칙 | 예시 |
|------|------|------|
| 카테고리 | PascalCase | `Dialogue/`, `Phone/`, `Schedule/` |
| 이름 | PascalCase + 언더스코어 | `Phone_ChatRoom`, `Log_Entry_Char` |

### 카테고리별 폴더
| 폴더 | 용도 |
|------|------|
| `Common/` | 공통 컴포넌트 |
| `Dialogue/` | 대화 UI |
| `Log/` | 백로그 UI |
| `Menu/` | 설정/저장/추가메뉴 |
| `Phone/` | 메신저 UI |
| `Schedule/` | 스케줄/상점 UI |
| `Stage/` | 스테이지 프리팹 |
| `Scene/` | 타이틀/이름입력 |
| `Top/` | 글로벌 팝업 |

---

## 10. 스토리 CSV

### 구조
```
Assets/Resources/Story/{FileName}.csv
```

### 규칙
| 유형 | 패턴 | 예시 |
|------|------|------|
| 프롤로그 | `Prologue.csv` | `Prologue.csv` |
| 데이타임 | `Day{N}_{Session}.csv` | `Day1_Morning.csv`, `Day1_Evening.csv` |
| 이벤트 | `Event{N}.csv` | `Event1.csv`, `Event2.csv` |
| 특별 | `{Name}.csv` | `Festival.csv`, `MT.csv` |
| 엔딩 | `Ending_{Character}.csv` | `Ending_Bom.csv` |

---

## 11. ScriptableObject 데이터

### 구조
```
Assets/Data/Characters/Character_{Name}.asset
Assets/Resources/Data/{Name}.asset
```

### 규칙
| 유형 | 패턴 | 예시 |
|------|------|------|
| 캐릭터 | `Character_{Name}.asset` | `Character_Bom.asset` |
| 게임 데이터 | `{Name}.asset` | `GameBalance.asset`, `ItemCatalog.asset` |

---

## AI를 위한 체크리스트

### 새 에셋 추가 시
- [ ] 명명 규칙 준수 (접두어, 케이스, 확장자)
- [ ] 해당 레지스트리 파일에 항목 추가
- [ ] `asset-registry.json`의 `status_summary` 업데이트
- [ ] CSV에서 참조하는 경우 올바른 형식 확인

### 에셋 수정 시
- [ ] 기존 파일명 변경 시 모든 CSV/코드 참조 확인
- [ ] 레지스트리 파일 동기화
- [ ] Unity `.meta` 파일 함께 커밋

---

## 위반 사례 (개선 필요)

| 현재 | 문제 | 권장 |
|------|------|------|
| `3clolor pen.png` | 오타 + 공백 | `3color_pen.png` |
| `marron bread.png` | 공백 | `marron_bread.png` |
| `상점목업 1.png` | 한글 | `shop_mockup_1.png` |
| `CD_tropical glow.png` | 대소문자 혼합 + 공백 | `cd_tropical_glow.png` |
