using UnityEngine;
using LoveAlgo.Game; // SceneFlowController

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 잔존 씬 중화 헬퍼. GameScene 계열 테스트가 Game.unity를 Single 로드 후 언로드하지 않아
    /// (HANDOFF "PlayMode 격리 주의") 뒤이은 테스트에도 씬 상주 구독자가 살아 있다. 특히
    /// <see cref="SceneFlowController"/>는 QuitGameCommand에 **에디터 PlayMode 정지**로 응답해
    /// 테스트 런 전체를 끊고, Start/Continue/Load/ReturnToTitle엔 씬 로드로 응답해 뒤 테스트를 오염시킨다.
    /// 씬 전환/종료 명령을 발행하는 테스트는 발행 전 반드시 이걸 호출한다(OnDisable=구독 해제,
    /// 다음 씬 Single 로드 시 새 인스턴스라 영구 영향 없음).
    /// </summary>
    public static class ResidentSceneGuard
    {
        public static void DisableSceneFlowControllers()
        {
            foreach (var c in Object.FindObjectsByType<SceneFlowController>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                c.enabled = false;
        }
    }
}
