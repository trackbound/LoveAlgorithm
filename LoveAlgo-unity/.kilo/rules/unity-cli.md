# unity-cli 사용 규칙

> Auto Refresh 비활성화됨. 코드 수정 후 반드시 수동 리프레시.

## 코드 수정 후 필수 플로우

```bash
# 1. 리컴파일 (여러 파일 수정 시 마지막에 한 번만)
unity-cli editor refresh --compile

# 2. 에러 확인
unity-cli console --type error
```

## 상태 확인

```bash
unity-cli status                    # Unity 연결 상태
unity-cli exec "return Application.dataPath;"   # 경로 확인
unity-cli exec "return EditorSceneManager.GetActiveScene().name;"  # 현재 씬
```

## 플레이모드

```bash
unity-cli editor play --wait        # 플레이 시작 (준비 대기)
unity-cli editor stop               # 플레이 중지
```

## 디버깅

```bash
unity-cli screenshot --view game    # 게임 뷰 캡처
unity-cli screenshot --view scene   # 씬 뷰 캡처
unity-cli profiler hierarchy --depth 3  # 프로파일러
```

## 에이전트별 사용 지침

| 에이전트 | 주요 명령 |
|---------|----------|
| code / plan | `refresh --compile` → `console --type error` |
| story-writer | 사용 안 함 (CSV만 편집) |
| asset-analyzer | `exec`로 에셋 DB 조회, 스크린샷 |
| unity-qa | `play --wait` → `screenshot` → `stop` |
