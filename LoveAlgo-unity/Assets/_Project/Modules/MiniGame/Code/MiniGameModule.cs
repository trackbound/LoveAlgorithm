using LoveAlgo.Contracts;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.MiniGame
{
    /// <summary>
    /// 미니게임 모듈 진입점.
    /// MiniGameLauncher(static)를 IMiniGame 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/MiniGameModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class MiniGameModule : MonoBehaviour, IMiniGame
    {
        void Awake() => Services.Register<IMiniGame>(this);

        void OnDestroy()
        {
            if (Services.TryGet<IMiniGame>() == (IMiniGame)this)
                Services.Unregister<IMiniGame>();
        }

        public bool IsRunning => MiniGameLauncher.IsRunning;

        public UniTask<int> LaunchAsync(string gameName, string heroineId)
            => MiniGameLauncher.LaunchAsync(gameName, heroineId);
    }
}
