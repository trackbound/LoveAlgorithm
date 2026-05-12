using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// PC잠금 모듈 진입점.
    /// LockScreenController를 ILockScreen으로 노출.
    /// 씬 하이어라키: _Modules/LockScreenModule (LockScreenController 컴포넌트 첨부)
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(LockScreenController))]
    public class LockScreenModule : MonoBehaviour
    {
        LockScreenController controller;

        void Awake()
        {
            controller = GetComponent<LockScreenController>();
            if (controller != null)
                Services.Register<ILockScreen>(controller);
        }

        void OnDestroy()
        {
            if (controller != null && Services.TryGet<ILockScreen>() == (ILockScreen)controller)
                Services.Unregister<ILockScreen>();
        }
    }
}
