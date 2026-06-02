using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events;  // EnteredEndingEvent
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 엔딩 화면(*Panel). EnteredEndingEvent를 구독해 엔딩 루트를 켜고 결과를 표시한다(ADR-007: 표시만).
    /// 30일 루프의 종료점 — 화려한 연출·엔딩 분기는 범위 밖(내러티브/M5 후속).
    /// </summary>
    public class EndingPanel : MonoBehaviour
    {
        [Tooltip("엔딩 비주얼 루트. 평소 비활성, 엔딩 진입 시 활성화.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text resultText;

        IDisposable _sub;

        public GameObject Root { get => root; set => root = value; }
        public TMP_Text ResultText { get => resultText; set => resultText = value; }
        public bool IsShown => root != null && root.activeSelf;

        void OnEnable() => _sub = EventBus.Subscribe<EnteredEndingEvent>(OnEnteredEnding);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        void OnEnteredEnding(EnteredEndingEvent e)
        {
            if (root != null) root.SetActive(true);
            // e.Day = MaxDay + 1 → 도달 일차는 그 직전.
            if (resultText != null) resultText.text = $"{e.Day - 1}일의 여정이 끝났습니다.";
        }
    }
}
