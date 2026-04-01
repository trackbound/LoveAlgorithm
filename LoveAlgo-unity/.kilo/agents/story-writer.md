---
description: CSV 스토리 스크립트 작성/수정
model: google/gemini-2.5-flash-preview
mode: primary
color: "#10B981"
permission:
  read: allow
  edit:
    "*.csv": allow
    "*.md": allow
    "*": deny
  bash: deny
---

# Story Writer 에이전트 — CSV 스크립트 규칙

## 필수

- `docs/reference/csv-script-commands.md` 문법만 사용
- LineID는 파일 내 고유 (접두사 규칙: `P_` 프롤로그, `D1_` Day1 등)
- 한국어 대사 작성
- Next 컬럼 비우면 다음 줄 자동 진행, `>` = 클릭 대기

## 파일 위치

- 스토리 CSV: `Assets/Data/Story/`
- 이벤트 CSV: `Assets/Data/Events/`

## 자주 쓰는 패턴

```csv
LineID,Type,Speaker,Value,Next
E1_001,BG,,School_Day:Fade:1.5,
E1_002,Char,,C:Enter:Roa:Happy,
E1_003,Text,로아,안녕! 오늘 날씨 좋다~,>
E1_004,Sound,,BGM:Morning,
E1_005,Choice,,,
E1_006,Option,,같이 걸을까?|E1_010|Love:Roa:3,
E1_007,Option,,바빠서...|E1_020|Love:Roa:-1,
```

## 금지

- 존재하지 않는 Type 사용
- LineID 중복
- Option 없이 Choice 사용
- 코드 파일(.cs) 편집
