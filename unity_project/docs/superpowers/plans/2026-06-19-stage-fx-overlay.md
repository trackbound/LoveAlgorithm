# 스테이지 투명 오버레이 FX (StageFx) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스토리 스크립트 한 줄(`StageFx:서류정리`)로 캐릭터(ROA) 위·대사 UI 아래에 투명 영상 효과를 논블로킹 재생하고, 효과가 도는 동안 기존 `Char ... Emote` 명령으로 표정을 교체한다.

**Architecture:** 기존 FX 패턴(ADR-007: 순수 파서 → 구조체 이벤트 → UI 뷰, Service Locator·UI 직접참조 없음)을 그대로 따른다. `StageFxOverlayParser`(순수)가 Value를 분해 → `NarrativeController.PlayFx` 캐스케이드가 `PlayStageFxCommand` 발행(즉시 다음 줄로 진행하는 논블로킹) → `StageFxOverlayView`가 전용 캔버스(order -5)에서 `Resources/Animation/{이름}` 투명 클립을 알파 RawImage로 재생·자동종료.

**Tech Stack:** Unity 6 LTS, C#, UnityEngine.Video.VideoPlayer, NUnit(EditMode), ffmpeg(자산 변환, VP8 알파 webm).

## Global Constraints

- 위험도 🟠 High — PlayFx 통합 diff 검토 + 엣지 자가검증 동봉.
- **기존 기능 무손상**: 풀스크린 `Video` 명령/`VideoView`는 손대지 않는다.
- Obsolete API 금지(Unity 6). 로깅은 `LoveAlgo.Common.Log`(`Log.Info`/`Log.Warn`).
- 피처 간 직접 참조 금지 — Core의 이벤트만 경유. 파서는 UnityEngine 비의존(EditMode 테스트 가능).
- 캔버스 sortingOrder: `_Stage`=-10(캐릭터), 그 외 모든 캔버스 ≥0 → 오버레이 캔버스 order = **-5**(캐릭터 위, 모든 UI 아래).
- 자산 경로 컨벤션: `Resources/Animation/{이름}`. 효과 파일명 = `서류정리`.
- ffmpeg는 PATH에 없을 수 있음 — WinGet shim: `C:/Users/podola/AppData/Local/Microsoft/WinGet/Links` 를 PATH에 추가해 사용.
- 한 기능 = 한 커밋. 커밋 메시지 본문에 "왜" 명시. 작업트리에 무관한 미커밋 변경 다수 — **각 커밋은 해당 작업 파일만** `git add`.

---

### Task 1: `StageFxOverlayParser` (순수 파서 + EditMode 테스트)

**Files:**
- Create: `Assets/_Project/Scripts/Narrative/StageFxOverlayParser.cs`
- Test: `Assets/Tests/EditMode/StageFxOverlayParserTests.cs`

**Interfaces:**
- Produces:
  - `LoveAlgo.Story.StageFxIntent` — `struct { bool IsValid; string Name; bool Loop; }`
  - `LoveAlgo.Story.StageFxOverlayParser.Parse(string value) -> StageFxIntent`
  - 문법: `StageFx:<파일명>[:Loop]` (head 케이스무시, `Loop` 플래그 케이스무시·순서무관, 파일명 trim). 비-`StageFx` head 또는 파일명 없음 → `IsValid=false`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/StageFxOverlayParserTests.cs`:

```csharp
using NUnit.Framework;
using LoveAlgo.Story; // StageFxOverlayParser, StageFxIntent

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 순수 <see cref="StageFxOverlayParser"/> 검증: <c>StageFx:파일명[:Loop]</c> 파싱과,
    /// 비-StageFx는 IsValid=false로 스킵 위임하는지. 기본 Loop=false·케이스무시·trim 확인.
    /// </summary>
    [TestFixture]
    public class StageFxOverlayParserTests
    {
        [Test]
        public void NameOnly_Defaults_NoLoop()
        {
            var v = StageFxOverlayParser.Parse("StageFx:서류정리");
            Assert.IsTrue(v.IsValid);
            Assert.AreEqual("서류정리", v.Name);
            Assert.IsFalse(v.Loop);
        }

        [Test]
        public void Loop_Flag_Sets_Loop()
        {
            var v = StageFxOverlayParser.Parse("StageFx:서류정리:Loop");
            Assert.IsTrue(v.IsValid);
            Assert.IsTrue(v.Loop);
        }

        [Test]
        public void CaseInsensitive_Command_And_Flag()
        {
            var v = StageFxOverlayParser.Parse("stagefx:Clip:loop");
            Assert.IsTrue(v.IsValid);
            Assert.AreEqual("Clip", v.Name);
            Assert.IsTrue(v.Loop);
        }

        [Test]
        public void Trims_Whitespace_Around_Name()
        {
            var v = StageFxOverlayParser.Parse("StageFx:  서류정리  ");
            Assert.AreEqual("서류정리", v.Name);
        }

        [Test]
        public void Missing_Name_Is_Invalid()
        {
            Assert.IsFalse(StageFxOverlayParser.Parse("StageFx").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("StageFx:").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("StageFx:   ").IsValid);
        }

        [Test]
        public void NonStageFx_Is_Invalid()
        {
            Assert.IsFalse(StageFxOverlayParser.Parse("Video:intro").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse(null).IsValid);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Unity Test Runner(EditMode) 또는 MCP `run_tests`로 `StageFxOverlayParserTests` 실행.
Expected: 컴파일 실패("StageFxOverlayParser/StageFxIntent 정의 없음") — 즉 RED.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/_Project/Scripts/Narrative/StageFxOverlayParser.cs` (`VideoParser`와 동형):

```csharp
using System;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스테이지 투명 오버레이 FX Value 순수 파서. EventBus·UnityEngine 비의존(EditMode 테스트). 형식
    /// <c>StageFx:파일명[:Loop]</c> — 파일명 뒤 토큰은 순서무관·케이스무시 플래그. 기본 Loop=false(1회 재생 후
    /// 자동 종료). 비-StageFx이거나 파일명 없으면 IsValid=false(PlayFx 캐스케이드의 다른 파서처럼 자기 head를
    /// 스스로 검사해 스킵 위임). 엔진(NarrativeController)이 결과를 <c>PlayStageFxCommand</c>로 발행하고,
    /// StageFxOverlayView가 Resources/Animation/{파일명} 투명 클립을 캐릭터 위·대사 아래에 재생한다.
    /// </summary>
    public static class StageFxOverlayParser
    {
        public static StageFxIntent Parse(string value)
        {
            var r = new StageFxIntent();
            if (string.IsNullOrEmpty(value)) return r;

            string[] parts = value.Split(':');
            if (parts.Length < 2) return r;
            if (!string.Equals(parts[0].Trim(), "StageFx", StringComparison.OrdinalIgnoreCase)) return r;

            r.Name = parts[1].Trim();
            if (string.IsNullOrEmpty(r.Name)) return r;

            for (int i = 2; i < parts.Length; i++)
            {
                string tok = parts[i].Trim();
                if (string.Equals(tok, "Loop", StringComparison.OrdinalIgnoreCase)) r.Loop = true;
            }

            r.IsValid = true;
            return r;
        }
    }

    /// <summary>StageFx 분해 결과. Name(Resources/Animation/{Name})·Loop(기본 false).</summary>
    public struct StageFxIntent
    {
        public bool IsValid;
        public string Name;
        public bool Loop;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

EditMode 테스트 재실행 후 `read_console`로 컴파일 에러 없음 확인.
Expected: `StageFxOverlayParserTests` 6개 전부 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Narrative/StageFxOverlayParser.cs Assets/_Project/Scripts/Narrative/StageFxOverlayParser.cs.meta Assets/Tests/EditMode/StageFxOverlayParserTests.cs Assets/Tests/EditMode/StageFxOverlayParserTests.cs.meta
git commit -m "feat(narrative): StageFxOverlayParser — StageFx:이름[:Loop] 순수 파서

왜: 투명 오버레이 효과를 스토리 한 줄로 트리거하려면 기존 FX들과 동일한
순수 파서 계층이 필요(EditMode 검증·UnityEngine 비의존).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: `PlayStageFxCommand` 이벤트 + `StageFxOverlayView` (오버레이 뷰)

**Files:**
- Create: `Assets/_Project/Scripts/Core/Events/StageFxEvents.cs`
- Create: `Assets/_Project/Scripts/UI/StageFxOverlayView.cs`

**Interfaces:**
- Consumes: 없음(자가구성).
- Produces:
  - `LoveAlgo.Events.PlayStageFxCommand` — `readonly struct { string Name; bool Loop; }`, ctor `(string name, bool loop)`.
  - `LoveAlgo.UI.StageFxOverlayView : MonoBehaviour` — `PlayStageFxCommand` 구독, `Resources/Animation/{Name}` 재생.

> 뷰는 MonoBehaviour+VideoPlayer라 단위 테스트 대신 **에디터 동작 검증**(Task 4 e2e). 이 태스크는 컴파일 통과까지가 게이트.

- [ ] **Step 1: 이벤트 구조체 작성**

Create `Assets/_Project/Scripts/Core/Events/StageFxEvents.cs`:

```csharp
namespace LoveAlgo.Events
{
    // ── 스테이지 투명 오버레이 FX 명령 ──
    // 캐릭터 위·대사 UI 아래에 투명 영상 효과를 얹는다(qtrle 소스는 VideoPlayer 재생 불가라
    // VP8 알파 webm으로 변환해 사용). 논블로킹 — 엔진(NarrativeController)이 이 명령을 발행한 뒤
    // 곧장 다음 줄로 진행하므로 완료 핸들이 없다(효과가 도는 동안 Char Emote가 인터리브).
    // 풀스크린 불투명 PlayVideoCommand/VideoView와 별개 경로(레이어·블로킹·탭처리 모두 다름).

    /// <summary>스테이지 투명 오버레이 FX 재생 명령. <see cref="Name"/>=Resources/Animation/{Name}. Loop=무한 유지(기본 1회).</summary>
    public readonly struct PlayStageFxCommand
    {
        public readonly string Name;
        public readonly bool Loop;

        public PlayStageFxCommand(string name, bool loop)
        {
            Name = name;
            Loop = loop;
        }
    }
}
```

- [ ] **Step 2: 오버레이 뷰 작성**

Create `Assets/_Project/Scripts/UI/StageFxOverlayView.cs` (`VideoView` 패턴, 단 논블로킹·투명·탭통과·오디오 무간섭):

```csharp
using System;
using System.Collections;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // PlayStageFxCommand, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 스테이지 투명 오버레이 FX 뷰(*View: StageFx). <see cref="PlayStageFxCommand"/>를 구독해
    /// Resources/Animation/{Name} 투명 클립을 캐릭터(_Stage, order -10) 위·대사 UI(order ≥0) 아래
    /// (전용 캔버스 order -5)에 알파 블렌딩으로 재생한다. 논블로킹 — 엔진이 발행 후 곧장 다음 줄로
    /// 진행하므로 이 뷰는 재생·자동종료만 담당(표정 명령이 효과 위로 인터리브). 자가구성(VideoView 패턴):
    /// Awake에서 Canvas+RawImage+VideoPlayer+RenderTexture 구성(인스펙터 바인딩 불필요, 씬엔 GO 하나만).
    /// 탭은 통과(raycastTarget=false, 스킵 아님) · 게임 오디오 무간섭(부가 효과). 클립 없거나 준비 실패 시
    /// 경고 후 즉시 종료(hang 0). 내러티브 종료/도구 화면정리 시 정리. RenderTexture는 OnDestroy에서 Release.
    /// </summary>
    public class StageFxOverlayView : MonoBehaviour
    {
        const string ResourceFolder = "Animation"; // Resources/Animation/{name}
        const int SortingOrder = -5;               // _Stage(-10) 위, 모든 UI(≥0) 아래
        const float PrepareTimeout = 8f;           // 준비 상한(코덱 불량/누락 시 무한대기 방지)

        Canvas _canvas;
        RawImage _image;
        VideoPlayer _player;
        RenderTexture _rt;

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        bool _reachedEnd;

        void Awake()
        {
            BuildHierarchy();
            HideImmediate();
        }

        void OnEnable()
        {
            _sub = EventBus.Subscribe<PlayStageFxCommand>(OnPlay);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetView());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetView());
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
        }

        // ── 자가 구성(Awake 1회) ──
        void BuildHierarchy()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            if (gameObject.GetComponent<CanvasScaler>() == null)
                gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var imgGo = new GameObject("StageFxImage");
            imgGo.transform.SetParent(transform, false);
            var rect = imgGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _image = imgGo.AddComponent<RawImage>();
            _image.color = Color.white;
            _image.raycastTarget = false; // 탭 통과(대사 진행 막지 않음 — 스킵 아님)
            _image.enabled = false;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.audioOutputMode = VideoAudioOutputMode.None; // 부가 효과 — 게임 오디오 무간섭(소스도 무음)
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.waitForFirstFrame = true;
            _player.skipOnDrop = true;
            _player.loopPointReached += OnLoopPointReached;
        }

        // ── 재생 ──
        void OnPlay(PlayStageFxCommand e)
        {
            if (_player == null) return; // 방어(Awake 전 발행)

            var clip = Resources.Load<VideoClip>($"{ResourceFolder}/{e.Name}");
            if (clip == null)
            {
                Log.Warn($"[StageFxOverlayView] 효과 없음: Resources/{ResourceFolder}/{e.Name} — 건너뜀");
                return; // 논블로킹 — 엔진은 이미 다음 줄로 진행
            }

            if (_routine != null) { StopCoroutine(_routine); _routine = null; }

            _player.Stop();
            EnsureRenderTexture((int)clip.width, (int)clip.height);
            _player.clip = clip;
            _player.isLooping = e.Loop;
            _player.targetTexture = _rt;
            _image.texture = _rt;

            _reachedEnd = false;
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(PlayStageFxCommand e)
        {
            _player.Prepare();
            float t = 0f;
            while (!_player.isPrepared && t < PrepareTimeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (!_player.isPrepared)
            {
                Log.Warn($"[StageFxOverlayView] 효과 준비 시간초과({PrepareTimeout}s) — 건너뜀: {e.Name}");
                HideImmediate();
                _routine = null;
                yield break;
            }

            _image.enabled = true;
            _player.Play();

            if (e.Loop) { _routine = null; yield break; } // 루프는 Reset(종료/도구 정리)까지 배경 유지

            while (!_reachedEnd) yield return null;
            HideImmediate();
            _routine = null;
        }

        void OnLoopPointReached(VideoPlayer _) => _reachedEnd = true;

        // 내러티브 종료/도구 화면정리 — 진행 중이면 끊고 정리.
        void ResetView()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_player != null) _player.Stop();
            HideImmediate();
        }

        void HideImmediate()
        {
            if (_image != null) _image.enabled = false;
            if (_player != null) _player.targetTexture = null;
        }

        void EnsureRenderTexture(int w, int h)
        {
            if (w <= 0) w = 1;
            if (h <= 0) h = 1;
            if (_rt != null && _rt.width == w && _rt.height == h) return;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            _rt.Create();
        }

        void OnDestroy()
        {
            if (_player != null) _player.loopPointReached -= OnLoopPointReached;
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` 후 `read_console`로 컴파일 에러 없음 확인.
Expected: 에러 없음. (`NarrativeFinishedEvent`/`ResetNarrativeViewsCommand`는 `Core/Events/NarrativeEvents.cs`에 기존재, `LoveAlgo.UI` asmdef는 `LoveAlgo.Core` 참조 — VideoView와 동일 조건.)

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Core/Events/StageFxEvents.cs Assets/_Project/Scripts/Core/Events/StageFxEvents.cs.meta Assets/_Project/Scripts/UI/StageFxOverlayView.cs Assets/_Project/Scripts/UI/StageFxOverlayView.cs.meta
git commit -m "feat(ui): StageFxOverlayView + PlayStageFxCommand — 투명 오버레이 FX 뷰

왜: 캐릭터 위·대사 아래(캔버스 order -5)에 투명 영상을 알파 합성하는
논블로킹 뷰가 필요. 풀스크린 불투명 VideoView와 레이어·블로킹·탭처리가
달라 별도 경로로 분리(VideoView는 무손상).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: `NarrativeController.PlayFx` 캐스케이드 통합

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/NarrativeController.cs` (PlayFx 메서드, `Video` 캐스케이드 항목 바로 위)

**Interfaces:**
- Consumes: `StageFxOverlayParser.Parse` (Task 1), `PlayStageFxCommand` (Task 2), 기존 `WaitNext(line, Func<bool>)`.
- Produces: 없음(통합 지점).

- [ ] **Step 1: 캐스케이드 항목 추가**

`PlayFx` 메서드 내 `// 영상(Video) — ...` 주석으로 시작하는 `var video = VideoParser.Parse(line.Value);` 블록 **바로 위**에 다음을 삽입:

```csharp
            // 투명 오버레이 FX(StageFx) — Resources/Animation/{이름} 투명 클립을 캐릭터 위·대사 아래에 재생.
            // 논블로킹: 명령만 발행하고 곧장 다음 줄로(효과 도는 동안 Char Emote 인터리브). 풀스크린 Video와 별개 경로.
            var stageFx = StageFxOverlayParser.Parse(line.Value);
            if (stageFx.IsValid)
            {
                EventBus.Publish(new PlayStageFxCommand(stageFx.Name, stageFx.Loop));
                yield return WaitNext(line, () => true);
                yield break;
            }

```

> 순서: `StageFx`가 `Video`보다 먼저 검사되지만 `VideoParser`는 head가 `Video`일 때만 유효하므로 충돌 없음. `using LoveAlgo.Story;`·`using LoveAlgo.Events;`는 파일 상단에 이미 존재(기존 파서/이벤트 사용 중).

- [ ] **Step 2: 컴파일 확인**

`refresh_unity` 후 `read_console`.
Expected: 에러 없음.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Narrative/NarrativeController.cs
git commit -m "feat(narrative): PlayFx에 StageFx 오버레이 캐스케이드 추가

왜: StageFx:이름[:Loop] 한 줄로 투명 오버레이를 트리거. 논블로킹이라
명령 발행 후 즉시 다음 줄로 진행해 효과 도는 동안 표정(Char Emote)이
인터리브된다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: webm 자산 변환 + 씬 배선 + 엔드투엔드 검증

**Files:**
- Create: `Assets/Resources/Animation/서류정리.webm` (+ Unity 생성 `.meta`)
- Delete: `Assets/Resources/Animation/2_file.mov` (+ `.meta`)  — qtrle라 재생 불가, 미참조 확인됨
- Modify: `Assets/_Project/Scenes/Game.unity` (StageFxOverlay GameObject 추가)

**Interfaces:**
- Consumes: Task 1~3 전체.

- [ ] **Step 1: webm 변환(VP8 알파)**

Git Bash에서 (ffmpeg PATH 추가 후) 소스를 Resources로 변환:

```bash
export PATH="/c/Users/podola/AppData/Local/Microsoft/WinGet/Links:$PATH"
cd "C:/Users/podola/LoveAlgorithm/unity_project"
ffmpeg -y -i "Assets/서류정리/서류정리효과.mov" \
  -c:v libvpx -pix_fmt yuva420p -auto-alt-ref 0 -b:v 2M -an \
  "Assets/Resources/Animation/서류정리.webm"
ffprobe -v error -show_entries stream=codec_name,width,height -of default=noprint_wrappers=1 "Assets/Resources/Animation/서류정리.webm"
```
Expected: `codec_name=vp8`, `1920x1080`, 파일 생성(~2–3MB). (알파는 webm 컨테이너 별도 레이어 — ffprobe pix_fmt엔 yuv420p로 보여도 Unity가 읽음.)

- [ ] **Step 2: 재생 불가 자산 제거**

```bash
cd "C:/Users/podola/LoveAlgorithm/unity_project"
git rm "Assets/Resources/Animation/2_file.mov" "Assets/Resources/Animation/2_file.mov.meta"
```
Expected: 두 파일 스테이징 삭제. (미참조 확인됨 — grep 결과 0건.)

- [ ] **Step 3: Unity 임포트 + 씬에 오버레이 GO 추가**

1. `refresh_unity`로 `서류정리.webm`을 VideoClip으로 임포트. `read_console`로 임포트 에러 없음 확인.
2. `Game.unity` 씬을 열고, 기존 `VideoView` GameObject와 동형으로 빈 GameObject `StageFxOverlay`(씬 루트, active)를 만들고 `StageFxOverlayView` 컴포넌트를 추가(뷰가 Awake에서 캔버스를 자가구성하므로 컴포넌트만 있으면 됨). 씬 저장.

> MCP로: `manage_gameobject`(create, name=`StageFxOverlay`) → `manage_components`(add `LoveAlgo.UI.StageFxOverlayView`) → `manage_scene`(save).

- [ ] **Step 4: 엔드투엔드 에디터 검증**

임시 검증용 스토리 줄을 사용(기존 테스트 스토리에 추가하거나 DevTools로):
```
StageFx:서류정리
Char:C:로아:기본
Wait:3
Char:C:로아:활짝
```
Play 모드에서 확인(체크리스트):
- [ ] 효과가 **캐릭터 위·대사창 아래**에 투명하게 합성된다(폴더/창/반짝임이 보이고, 캐릭터·대사 가리지 않음).
- [ ] 효과 가장자리에 검은 박스(불투명 배경)가 **없다** → 알파 정상. (검은 사각이 보이면 알파 미적용 — 아래 노트 참조.)
- [ ] 효과 도중 `Char Emote`로 표정이 바뀐다(논블로킹).
- [ ] ~10.5초 후 효과가 자동으로 사라진다.
- [ ] 효과 영역 탭이 대사 진행으로 통과된다(효과가 탭을 먹지 않음).
- [ ] 존재하지 않는 이름(`StageFx:없는효과`)일 때 경고 로그 + hang 없이 다음 줄 진행.

> **알파가 안 나올 경우 폴백**: RawImage가 검은 배경을 보이면, (a) `RenderTextureFormat.ARGB32` 확인, (b) VP8 알파가 Unity에서 안 잡히는 환경이면 변환을 `libvpx-vp9 -pix_fmt yuva420p`로 재시도. 그래도 안 되면 스펙 §8(PNG 시퀀스) 폴백을 감독에게 보고.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/podola/LoveAlgorithm/unity_project"
git add "Assets/Resources/Animation/서류정리.webm" "Assets/Resources/Animation/서류정리.webm.meta" "Assets/_Project/Scenes/Game.unity"
git add -u "Assets/Resources/Animation/2_file.mov" "Assets/Resources/Animation/2_file.mov.meta"
git commit -m "feat(fx): 서류정리 투명 오버레이 자산(webm) + 씬 배선, 재생불가 2_file 제거

왜: qtrle/argb 소스는 VideoPlayer 재생 불가 → VP8 알파 webm으로 변환해
Resources/Animation에 배치하고 씬에 StageFxOverlay 뷰를 배선. 기존
재생 불가 2_file.mov(qtrle, 미참조)는 제거.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## 범위 밖 (후속 — 이 플랜에 포함 안 함)

- iOS HEVC+알파 자산 인코딩(Mac `hevc_videotoolbox`) 및 `#if UNITY_IOS` 클립 선택 실배선. iOS 자산 없는 동안 iOS 빌드는 효과만 스킵(클립 없음 → 경고 후 즉시 종료, hang 없음).
- 효과 위치/스케일 튜닝 SO(현재 풀스크린 고정).
- STORY_COMMANDS.md에 `StageFx` 문법 문서화(문서 작업).

## Self-Review

- **스펙 커버리지**: §3.1 파서→Task1, §3 이벤트/뷰→Task2, §3.2 엔진통합→Task3, §5 자산/2_file제거→Task4, §4 레이어(order -5)→Task2 상수, §7 테스트→Task1(EditMode)+Task4(e2e). iOS(§5)·튜닝(§8)은 명시적 범위 밖. ✅
- **플레이스홀더**: 없음 — 모든 코드/명령 전체 기재. ✅
- **타입 일관성**: `StageFxIntent{IsValid,Name,Loop}`·`StageFxOverlayParser.Parse`·`PlayStageFxCommand(string,bool)`·`StageFxOverlayView` 전 태스크 일치. `WaitNext(line, ()=>true)`는 기존 시그니처(`IEnumerator WaitNext(ScriptLine, Func<bool>)`)와 일치. ✅
