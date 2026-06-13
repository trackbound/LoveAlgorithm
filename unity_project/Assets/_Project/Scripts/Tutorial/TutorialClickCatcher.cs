using UnityEngine;
using UnityEngine.EventSystems;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 딤 위 클릭 수신 릴레이 — 딤 Image(raycast 타깃)에 붙어 클릭 좌표를 TutorialView로 넘긴다.
    /// 좌표가 필요해 Button 대신 IPointerClickHandler(강제 클릭 스텝의 앵커 영역 판정).
    /// </summary>
    public class TutorialClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] TutorialView view;

        public TutorialView View { get => view; set => view = value; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (view != null) view.HandleClick(eventData);
        }
    }
}
