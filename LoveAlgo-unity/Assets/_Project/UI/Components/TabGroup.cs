using System;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 탭 그룹 — ButtonEX(Toggle 모드)를 묶어 "하나만 선택" 규칙 적용
    ///
    /// 사용법:
    ///   1. 탭 버튼 부모에 TabGroup 추가
    ///   2. tabs[] 에 ButtonEX(Toggle 모드) 드래그
    ///   3. OnTabChanged 이벤트 구독 또는 Select() 호출
    /// </summary>
    public class TabGroup : MonoBehaviour
    {
        [SerializeField] ButtonEX[] tabs;
        [SerializeField] int defaultTab;

        int currentIndex = -1;

        /// <summary>탭 변경 시 콜백 (새 인덱스)</summary>
        public event Action<int> OnTabChanged;

        void Awake()
        {
            if (tabs == null) return;

            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                if (tabs[i] == null) continue;

                var btn = tabs[i].GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => Select(idx));
            }
        }

        void Start()
        {
            Select(defaultTab, notify: false);
        }

        /// <summary>탭 선택 (외부 호출용)</summary>
        public void Select(int index, bool notify = true)
        {
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            if (index == currentIndex) return;

            currentIndex = index;

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null)
                    tabs[i].SetToggle(i == index);
            }

            if (notify)
                OnTabChanged?.Invoke(index);
        }

        /// <summary>현재 선택된 탭 인덱스</summary>
        public int CurrentIndex => currentIndex;
    }
}
