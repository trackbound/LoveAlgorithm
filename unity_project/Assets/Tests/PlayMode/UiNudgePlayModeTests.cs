using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>UiNudge.Shake: rt를 잠시 흔든 뒤 기준 위치로 복원하는지(host 코루틴 구동).</summary>
    public class UiNudgePlayModeTests
    {
        class Host : MonoBehaviour { }

        [UnityTest]
        public IEnumerator Shake_Perturbs_ThenRestores()
        {
            var hostGo = new GameObject("Host"); var host = hostGo.AddComponent<Host>();
            var rtGo = new GameObject("W", typeof(RectTransform));
            var rt = rtGo.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(100f, 50f);
            var basePos = rt.anchoredPosition;
            Coroutine co = null;
            try
            {
                UiNudge.Shake(host, rt, ref co, 12f, 60f, 0.2f);
                yield return null; yield return null; // 흔들림 중
                Assert.AreNotEqual(basePos.x, rt.anchoredPosition.x, "흔들림 중엔 위치가 변함");
                yield return new WaitForSeconds(0.3f); // 종료 대기
                Assert.AreEqual(basePos, rt.anchoredPosition, "종료 후 기준 위치 복원");
            }
            finally { Object.DestroyImmediate(hostGo); Object.DestroyImmediate(rtGo); }
        }
    }
}
