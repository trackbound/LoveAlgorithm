using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowSettingsCommand(임의 구독 검증용 경량 커맨드)
using LoveAlgo.UI;     // UiBootActivator

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 활성화→OnEnable 구독 사슬 전체 검증: inactive로 저장된 홀더는 명령을 못 받고(죽은 UI 재현),
    /// UiBootActivator가 켜면 그 즉시 구독이 살아나 명령을 받는다 — "에디터=inactive 저장" 정책의
    /// 런타임 안전을 못박는 테스트.
    /// </summary>
    public class UiBootActivatorPlayModeTests
    {
        // OnEnable 구독 뷰의 최소 재현(프로젝트 전 뷰의 구독 패턴 미러).
        class SubscribeProbe : MonoBehaviour
        {
            public int Received;
            IDisposable _sub;
            void OnEnable() => _sub = EventBus.Subscribe<ShowSettingsCommand>(_ => Received++);
            void OnDisable() { _sub?.Dispose(); _sub = null; }
        }

        [UnityTest]
        public IEnumerator Activation_RevivesOnEnableSubscription()
        {
            var holder = new GameObject("InactiveOverlay");
            holder.SetActive(false); // 씬 inactive 저장 재현 — AddComponent해도 OnEnable 안 돈다
            var probe = holder.AddComponent<SubscribeProbe>();

            var activatorGo = new GameObject("Activator");
            var activator = activatorGo.AddComponent<UiBootActivator>(); // Awake(targets null) = no-op
            activator.Targets = new[] { holder };

            try
            {
                EventBus.Publish(new ShowSettingsCommand());
                Assert.AreEqual(0, probe.Received, "inactive 홀더 = 구독 없음(죽은 UI 재현)");

                activator.ActivateAll(); // 부팅 활성화 — SetActive가 OnEnable을 동기 실행
                yield return null;

                EventBus.Publish(new ShowSettingsCommand());
                Assert.IsTrue(holder.activeSelf, "활성화됨");
                Assert.AreEqual(1, probe.Received, "활성화 직후 구독이 살아나 명령 수신");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(activatorGo);
            }
        }
    }
}
