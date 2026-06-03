using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // ScreenPhase
using LoveAlgo.Events; // ScreenPhaseChangedEvent
using UnityEngine;
using UnityEngine.Serialization; // FormerlySerializedAs (구 narrativeRoot/simulationRoot 바인딩 보존)

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 매니저(승인된 4매니저 중 UIManager). 화면 그룹 루트를 제공하고, 그룹 단위 show/hide를
    /// <see cref="ScreenPhaseChangedEvent"/> 구독으로 처리한다(ADR-007·ADR-013) — 화면 전환의 권위는
    /// PhaseController이고, UIManager는 그 통지에 반응해 기계적으로 그룹을 토글할 뿐. 구 Service Locator·
    /// 서브UI 직접 참조(ShowOnly/wrapper)는 제거(금지선4). 구 동명 UIManager와는 autoRef=false로 격리.
    ///
    /// 그룹 = <see cref="ScreenPhase"/> 1:1(Story/Schedule/Ending). 대상만 활성·나머지 비활성이라 "두 화면 동시
    /// active"가 구조적으로 불가(엔딩 겹침 버그 해소). 미바인딩 그룹 루트는 자동 생성.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("화면 그룹 루트 (ScreenPhase별, 미바인딩 시 자동 생성)")]
        [FormerlySerializedAs("narrativeRoot")]  [SerializeField] Transform storyRoot;
        [FormerlySerializedAs("simulationRoot")] [SerializeField] Transform scheduleRoot;
        [SerializeField] Transform endingRoot;

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<ScreenPhaseChangedEvent>(e => ShowGroup(e.To));

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>화면 그룹 루트 — UI가 자기 인스턴스를 spawn할 때 사용(없으면 생성).</summary>
        public Transform GetGroupRoot(ScreenPhase group)
        {
            switch (group)
            {
                case ScreenPhase.Story:    return storyRoot    != null ? storyRoot    : EnsureGroup(ref storyRoot,    "Story",    0);
                case ScreenPhase.Schedule: return scheduleRoot != null ? scheduleRoot : EnsureGroup(ref scheduleRoot, "Schedule", 1);
                case ScreenPhase.Ending:   return endingRoot   != null ? endingRoot   : EnsureGroup(ref endingRoot,   "Ending",   2);
                default:                 return transform;
            }
        }

        /// <summary>대상 그룹만 활성, 나머지 비활성. 직접 호출 가능(라이프사이클 비의존 — 테스트/와이어링).</summary>
        public void ShowGroup(ScreenPhase target)
        {
            foreach (ScreenPhase g in (ScreenPhase[])Enum.GetValues(typeof(ScreenPhase)))
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
