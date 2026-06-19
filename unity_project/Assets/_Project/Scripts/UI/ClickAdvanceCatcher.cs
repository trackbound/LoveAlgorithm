using System;
using UnityEngine;
using UnityEngine.EventSystems; // IPointerClickHandler, PointerEventData

namespace LoveAlgo.UI
{
    /// <summary>
    /// 풀스크린 클릭 캐처(*첫실행 연출). 무장(Arm) 상태일 때만 클릭/탭을 받아 <see cref="Clicked"/>를 발화한다 —
    /// 무장 전 클릭은 삼켜서 무시(이른 진행 방지). 같은 GameObject에 레이캐스트 타깃 Graphic(투명 Image)을 두고
    /// 캔버스 최상단(마지막 형제)에 배치해 화면 어디를 눌러도 받는다. 표시·발행 분리(ADR-007): 여기선 신호만 올린다.
    /// </summary>
    public class ClickAdvanceCatcher : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>무장 상태에서 클릭이 들어오면 발화.</summary>
        public event Action Clicked;

        bool _armed;

        /// <summary>현재 클릭을 받는 상태인지.</summary>
        public bool Armed => _armed;

        /// <summary>클릭 수신 시작.</summary>
        public void Arm() => _armed = true;

        /// <summary>클릭 수신 중지(이후 클릭 무시).</summary>
        public void Disarm() => _armed = false;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_armed) Clicked?.Invoke();
        }
    }
}
