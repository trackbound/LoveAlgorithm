using System.Collections;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // PlayScriptCommand
using LoveAlgo.Story;  // StoryAssetLoader
using UnityEngine;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 부팅 컴포지션 루트(MonoBehaviour). 씬 시작 시 부팅 모드(<see cref="GameEntry"/>)에 따라 런타임을
    /// 초기화한다 — 새 게임(<see cref="GameBoot.NewGame"/>: 리셋+공식+1일차) 또는 이어하기
    /// (<see cref="GameBoot.ContinueGame"/>: 오토세이브 복원+공식). 매니저/컨트롤러의 State 바인딩은
    /// 인스펙터에서 같은 GameStateSO를 가리키게 두고, 이 컴포넌트는 인스펙터로 못 하는 런타임 단계만 담당한다.
    ///
    /// 새 게임이면 부팅 직후 프롤로그를 1회 자동 재생한다(<see cref="PlayPrologue"/>). 이어하기는 스킵.
    ///
    /// 씬 하이어라키: _Boot/GameBootstrap. <see cref="state"/>=공유 GameStateSO, <see cref="balance"/>=GameBalanceSO(null이면 폴백).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("매니저들과 공유하는 단일 GameStateSO.")]
        [SerializeField] GameStateSO state;
        [Tooltip("호감도 정의표 소스. 비우면 검증된 폴백 사용.")]
        [SerializeField] GameBalanceSO balance;
        [Tooltip("Start에서 자동 부팅(모드는 GameEntry.PendingMode — 기본 새 게임).")]
        [SerializeField] bool newGameOnStart = true;
        [Tooltip("새 게임 첫 진입 시 1회 재생할 프롤로그 CSV(StreamingAssets/Story/ 기준 파일명). 비우면 스킵.")]
        [SerializeField] string prologueCsv = "Prologue.csv";
        [Tooltip("저녁 이벤트 중 세이브 복원 위임 대상(같은 _Bootstrap의 GameManager). 비우면 씬에서 자동 탐색.")]
        [SerializeField] GameManager gameManager;
        [Tooltip("부팅(타이틀→게임 진입) 시 로딩 오버레이 표시 시간(초). 세이브 복원·스테이지 스냅 플래시를 덮는다. 0=끔.")]
        [SerializeField] float bootLoadingSeconds = 1.5f;

        public GameStateSO State { get => state; set => state = value; }
        public GameBalanceSO Balance { get => balance; set => balance = value; }
        public string PrologueCsv { get => prologueCsv; set => prologueCsv = value; }
        public GameManager Manager { get => gameManager; set => gameManager = value; }
        public float BootLoadingSeconds { get => bootLoadingSeconds; set => bootLoadingSeconds = value; }

        void Start()
        {
            if (newGameOnStart) Boot();
        }

        /// <summary>부팅 모드(GameEntry)를 소비해 새 게임/이어하기를 수행. 직접 호출 가능(테스트/명시적 진입).</summary>
        public void Boot()
        {
            if (state == null)
            {
                Debug.LogError("[GameBootstrap] state(GameStateSO) 미바인딩 — 부팅 불가.");
                return;
            }
            // 타이틀→게임 진입 로딩 비트: 아래 복원/프롤로그가 스테이지를 즉시(dur=0) 세팅하며 한 프레임
            // 깜빡이므로, 그 위를 로딩 오버레이로 덮는다(ADR-007: 발행만, LoadingScreenView가 표시·자동 숨김).
            // 이어하기는 이 핸들을 await하지 않는다 — 복원은 곧장 발행되고 오버레이가 그 위에서 시간만큼 가린다.
            CompletionHandle loadingHandle = null;
            if (bootLoadingSeconds > 0f)
            {
                loadingHandle = new CompletionHandle();
                EventBus.Publish(new ShowLoadingCommand(bootLoadingSeconds, loadingHandle));
            }

            int slot = GameEntry.SelectedSlot; // Consume이 리셋하므로 먼저 읽는다
            var mode = GameEntry.Consume();
            if (mode == BootMode.Continue && GameBoot.ContinueGame(state, balance, slot))
            {
                TryResumeStory(); // 스토리 중 세이브였다면 그 장면 재개(아니면 종전대로 스케줄)
                return;
            }
            GameBoot.NewGame(state, balance); // NewGame이거나 Continue 폴백(세이브 없음/손상)
            // 새 게임 프롤로그 첫 컷은 인트로 영상(VideoView order 32000 = 최상위)이라, 로딩과 동시에 발행하면
            // 영상이 로딩 오버레이를 즉시 덮어쓴다("바로 덮어버리는" 레이스). 로딩 완료까지 기다렸다 재생해
            // 로딩 → 영상 순서를 보장한다. 로딩이 없으면(핸들 null) 종전대로 즉시 재생.
            if (loadingHandle != null)
                StartCoroutine(PlayPrologueAfterLoading(loadingHandle));
            else
                PlayPrologue(); // 새 게임 1회: 프롤로그 자동 재생
        }

        /// <summary>로딩 오버레이(<paramref name="loadingHandle"/>)가 완료된 뒤 프롤로그를 발행 — 인트로
        /// 영상이 로딩을 즉시 덮지 않도록 순차화. 오버레이 미바인딩 시 핸들이 즉시 완료돼 다음 프레임 재생.</summary>
        IEnumerator PlayPrologueAfterLoading(CompletionHandle loadingHandle)
        {
            while (!loadingHandle.IsComplete) yield return null;
            PlayPrologue();
        }

        /// <summary>
        /// 스토리 위치 세이브 복원: 세이브의 storyScriptId가 비어있지 않으면 ① 무대 스냅샷 재현(해석된 코드ID라
        /// 별칭 재해석 없이 Setup 미러 — BG Cut/BGM/캐릭터 즉시) ② 저장 앵커(Text/Choice 라인)부터 스크립트 재개.
        /// "prologue"는 직접 발행(원 부팅 흐름과 동일 — 종료 시 스케줄 복귀), 그 외(=저녁 이벤트 csvPath)는
        /// GameManager 씨임으로 위임해 종료 후 하루 전환까지 원 흐름을 보존한다. 해석 실패는 fail-open
        /// (경고 후 위치를 비우고 스케줄 재개 — 진행이 막히지 않는다). 테스트 직호출 가능하게 public.
        /// </summary>
        public void TryResumeStory()
        {
            if (state == null) return;
            var d = state.Data;
            if (string.IsNullOrEmpty(d.storyScriptId)) return; // 스토리 밖 세이브 → 스케줄 재개(종전 동작)

            string id = d.storyScriptId;
            int index = d.storyLineIndex;

            // 무대 재현 — 스냅샷은 발행 시점 미러라 그대로 재발행(핸들은 뷰가 완료, 대기 불요).
            if (!string.IsNullOrEmpty(d.storyBg))
                EventBus.Publish(new ShowBackgroundCommand(d.storyBg, BgTransition.Cut, 0f, new CompletionHandle()));
            if (!string.IsNullOrEmpty(d.storyBgm))
                EventBus.Publish(new PlayBgmCommand(d.storyBgm));
            // 로아 디바이스 복원 — 로아 Char Enter 재발행 전에 쏴서 컨트롤러가 올바른 디바이스로 오버레이를
            // 재구성하게 한다(오버레이 이름은 별도 저장하지 않고 디바이스+표정으로 파생).
            if (!string.IsNullOrEmpty(d.storyRoaDevice) && RoaDeviceParse.TryParse(d.storyRoaDevice, out var roaDev))
                EventBus.Publish(new SetRoaDeviceCommand(roaDev));

            foreach (var c in d.storyChars)
            {
                if (c == null || string.IsNullOrEmpty(c.id)) continue;
                EventBus.Publish(new ShowCharacterCommand((CharSlot)c.slot, CharAction.Enter, c.id, c.emote, 0f, new CompletionHandle()));
            }

            // 연출 지속 상태 재현(스테이지 상태 세이브) — 발행 시점 미러라 dur=0 즉시 재발행.
            if (d.storyTintA > 0f)
                EventBus.Publish(new ColorTintCommand(d.storyTintR, d.storyTintG, d.storyTintB, d.storyTintA, 0f, false, new CompletionHandle()));
            if (!string.IsNullOrEmpty(d.storySd))
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.SD, false, d.storySd, LayerTransition.Cut, 0f, new CompletionHandle()));
            if (!string.IsNullOrEmpty(d.storyOverlay))
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, false, d.storyOverlay, LayerTransition.Cut, 0f, new CompletionHandle()));
            // CG 복원 — 캐릭터 위를 덮는 풀스크린 컷신. 재발행 시 StageLayerView가 SetCgModeCommand(true)도 함께
            // 발행해 대사창/메뉴를 CG 모드로 되돌린다(재개 Text 라인이 대사창을 다시 띄움). 아이마스크보다 먼저.
            if (!string.IsNullOrEmpty(d.storyCg))
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.CG, false, d.storyCg, LayerTransition.Cut, 0f, new CompletionHandle()));
            if (d.storyEyeClosed)
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.CloseImmediate, 0f, 0f, 0f, new CompletionHandle())); // 최상위 가림 → 마지막

            if (id == "prologue")
            {
                string csv = StoryAssetLoader.Read(prologueCsv);
                if (!string.IsNullOrEmpty(csv))
                {
                    EventBus.Publish(new PlayScriptCommand(csv, "prologue", index));
                    return;
                }
            }
            else
            {
                // 저녁 이벤트(storyScriptId=csvPath 규약) → GameManager 씨임 재진입(종료 시 하루 전환 보존).
                var gm = gameManager != null ? gameManager : FindAnyObjectByType<GameManager>();
                if (gm != null)
                {
                    gm.ResumeEveningEvent(id, index);
                    return;
                }
            }

            // fail-open: 해석 불가(파일 삭제/dev 스크립트 잔존/GameManager 부재) — 위치를 비우고 스케줄 재개.
            Log.Warn($"[GameBootstrap] 스토리 위치 복원 실패(scriptId='{id}') — 스케줄에서 재개.");
            d.storyScriptId = "";
            d.storyLineIndex = 0;
        }

        /// <summary>
        /// 새 게임 첫 진입 프롤로그를 발행한다(저녁이벤트와 같은 StoryAssetLoader→PlayScriptCommand 패턴).
        /// 순수 VN 전환 이후 부팅 기본 페이즈가 Story이므로(GameStateData.phase 기본값) NarrativeController.Run의
        /// 페이즈 요청은 이미-Story면 생략된다 — 스케줄 깜빡임 없음. CSV 없으면 fail-open
        /// (로그 후 스킵). 이어하기엔 호출되지 않는다(Boot의 early return).
        /// </summary>
        void PlayPrologue()
        {
            if (string.IsNullOrEmpty(prologueCsv)) return;
            string csv = StoryAssetLoader.Read(prologueCsv);
            if (string.IsNullOrEmpty(csv))
            {
                Log.Warn($"[GameBootstrap] 프롤로그 CSV 로드 실패 — 스킵: {prologueCsv}");
                return;
            }
            EventBus.Publish(new PlayScriptCommand(csv, "prologue"));
        }
    }
}
