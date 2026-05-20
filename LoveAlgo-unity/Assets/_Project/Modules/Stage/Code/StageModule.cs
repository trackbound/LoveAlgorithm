using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Stage
{
    /// <summary>
    /// Stage 모듈 진입점 — 씬 단일 인스턴스 + DI 등록.
    ///
    /// 역할:
    /// 1. <see cref="IStage"/> 구현 + <see cref="Services"/> 등록
    /// 2. 씬에 배치된 <see cref="StageRig"/> 인스턴스를 참조(인스펙터 바인딩 필수)
    /// 3. CharacterStageDatabase 시각 트랜스폼 DB 보유 (인스펙터 바인딩)
    ///
    /// 신규 호출자는 <c>Services.Get&lt;IStage&gt;().X</c> 권장.
    /// 정적 접근은 <c>StageModule.Instance.X</c> (SingletonMonoBehaviour).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class StageModule : SingletonMonoBehaviour<StageModule>, IStage
    {
        [Header("Stage Rig (씬 배치 인스턴스)")]
        [Tooltip("씬에 배치된 StageRig를 직접 바인딩. 비워두면 Awake 시점에 한 번만 FindAnyObjectByType으로 자동 탐색.")]
        [SerializeField] StageRig stageRig;

        [Header("Data")]
        [Tooltip("캐릭터별 시각 트랜스폼(스케일·오프셋·피벗) DB")]
        [SerializeField] CharacterStageDatabase characterStageDatabase;

        bool _destroyed;

        StageRig Rig
        {
            get
            {
                if (_destroyed) return null;
                if (stageRig == null)
                {
                    // 인스펙터 미바인딩 시 마지막 안전망: 씬에서 한 번 찾아본다.
                    stageRig = FindAnyObjectByType<StageRig>(FindObjectsInactive.Include);
                    if (stageRig == null)
                    {
                        Debug.LogError("[StageModule] StageRig가 씬에 없거나 인스펙터에 바인딩되지 않았습니다.");
                    }
                }
                return stageRig;
            }
        }

        protected override void OnSingletonAwake()
        {
            // 인스펙터 바인딩 미설정 시 1회만 자동 탐색
            if (stageRig == null)
                stageRig = FindAnyObjectByType<StageRig>(FindObjectsInactive.Include);

            if (stageRig == null)
                Debug.LogError("[StageModule] StageRig를 찾지 못했습니다. 씬에 배치 + 인스펙터 바인딩 권장.");

            Services.Register<IStage>(this);
        }

        protected override void OnDestroy()
        {
            _destroyed = true;
            if (Services.TryGet<IStage>() == (IStage)this)
                Services.Unregister<IStage>();
            base.OnDestroy();
        }

        // ─── IStage ──────────────────────────────────────────────
        public Canvas StageCanvas => Rig?.StageCanvas;
        public BackgroundLayer Background => Rig?.Background;
        public VirtualBGOverlay VirtualBG => Rig?.VirtualBG;
        public CharacterLayer Character => Rig?.Character;
        public MonologueDim MonologueDim => Rig?.MonologueDim;
        public SDCutsceneLayer SDCutscene => Rig?.SDCutscene;
        public CGLayer CG => Rig?.CG;
        public EyeMask EyeMask => Rig?.EyeMask;
        public CharacterStageDatabase CharacterStage => characterStageDatabase;

        public void CharacterEmote(string slot, string emote)
            => Character?.ChangeEmote(slot, emote);

        public void ClearAllCharacters()
            => Character?.ClearAll();
    }
}
