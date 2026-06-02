using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // UIGroup, ShowUiGroupCommand
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 매니저(승인된 4매니저 중 UIManager). UI 인스턴스 부모 그룹 루트를 제공하고, 그룹 단위 show/hide를
    /// <see cref="ShowUiGroupCommand"/> 구독으로 처리한다(ADR-007). 구 <c>LoveAlgo.UI.UIManager</c>의
    /// Service Locator·서브UI 직접 참조(ShowOnly/HideAll/wrapper)는 제거 — 그룹 루트 + 그룹 전환만(금지선4).
    ///
    /// 슬라이스1 범위: 그룹 루트 제공(미바인딩 시 자동 생성) + ShowGroup(대상 활성/나머지 비활성).
    /// 범위 밖(후속): MainUIType→특정 UI 매핑, 서브UI 정리, 시뮬레이션 컨텍스트 진입 등(모듈 이식 시).
    /// 구 UIManager와 같은 ns/이름이지만 autoRef=false라 동시 가시 컴파일 단위가 없어 충돌 없음.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI Group Roots (미바인딩 시 자동 생성)")]
        [SerializeField] Transform narrativeRoot;
        [SerializeField] Transform simulationRoot;
        [SerializeField] Transform titleRoot;

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<ShowUiGroupCommand>(e => ShowGroup(e.Group));

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>UI 인스턴스 부모 그룹 루트 — 모듈이 자기 UI를 spawn할 때 사용(없으면 생성).</summary>
        public Transform GetGroupRoot(UIGroup group)
        {
            switch (group)
            {
                case UIGroup.Narrative:  return narrativeRoot  != null ? narrativeRoot  : EnsureGroup(ref narrativeRoot,  "Narrative",  0);
                case UIGroup.Simulation: return simulationRoot != null ? simulationRoot : EnsureGroup(ref simulationRoot, "Simulation", 1);
                case UIGroup.Title:      return titleRoot      != null ? titleRoot      : EnsureGroup(ref titleRoot,      "Title",      2);
                default:                 return transform;
            }
        }

        /// <summary>대상 그룹만 활성, 나머지 그룹은 비활성. 직접 호출 가능(라이프사이클 비의존 — 테스트/와이어링).</summary>
        public void ShowGroup(UIGroup target)
        {
            foreach (UIGroup g in (UIGroup[])Enum.GetValues(typeof(UIGroup)))
                GetGroupRoot(g).gameObject.SetActive(g == target);
        }

        Transform EnsureGroup(ref Transform field, string groupName, int siblingIndex)
        {
            var existing = transform.Find(groupName);
            if (existing != null) { field = existing; return field; }

            var go = new GameObject(groupName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(siblingIndex);
            field = rt;
            return field;
        }
    }
}
