using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 타이틀 메뉴 버튼 호버 → "Highlights" 패널에서 같은 이름의 자식만 활성(나머지·Neutral 비활성).
    /// 아무 버튼도 호버하지 않으면 <see cref="neutralName"/> 자식(기본 "Neutral")을 활성으로 되돌린다.
    /// 자식 활성/비활성 = 스프라이트 스왑(자식마다 하이라이트 아트가 따로 들어있음).
    ///
    /// 매핑은 <b>이름 일치</b>로 자동 구성한다: <see cref="buttonsRoot"/> 하위 각 <see cref="Button"/>의
    /// GameObject 이름(예: "Start")으로 <see cref="highlightsRoot"/>에서 같은 이름의 자식을 찾는다 —
    /// 인스펙터 쌍 배선 불필요. 호버는 <see cref="ButtonSpriteSwap"/>과 동일하게 <b>raw 포인터 이벤트</b>로
    /// 잡으므로 "EventSystem 포커스(Selected)가 호버(Highlighted)를 가리는" 문제가 없다.
    /// </summary>
    public class TitleHighlightSwitcher : MonoBehaviour
    {
        [Tooltip("타이틀 버튼들을 담은 컨테이너(하위 Button을 모두 수집). 비우면 호버 연동 없음.")]
        [SerializeField] Transform buttonsRoot;
        [Tooltip("Neutral + 버튼별 하이라이트 자식이 들어있는 컨테이너. 비우면 이 오브젝트.")]
        [SerializeField] Transform highlightsRoot;
        [Tooltip("아무 버튼도 호버하지 않을 때 표시할 자식 이름.")]
        [SerializeField] string neutralName = "Neutral";

        readonly List<GameObject> _children = new(); // Highlights의 직계 자식 전부(Neutral 포함)
        GameObject _neutral;
        GameObject _current;                          // 현재 호버 대상(없으면 null → Neutral)

        void Awake()
        {
            if (highlightsRoot == null) highlightsRoot = transform;

            foreach (Transform c in highlightsRoot) _children.Add(c.gameObject);
            _neutral = FindChild(neutralName);

            if (buttonsRoot != null)
            {
                foreach (var btn in buttonsRoot.GetComponentsInChildren<Button>(true))
                {
                    var highlight = FindChild(btn.gameObject.name);
                    if (highlight == null) continue; // 대응 하이라이트 없는 버튼은 무시
                    var hover = btn.gameObject.AddComponent<TitleHighlightHover>();
                    hover.Init(
                        onEnter: () => { _current = highlight; Refresh(); },
                        onExit: () => { if (_current == highlight) { _current = null; Refresh(); } });
                }
            }

            Refresh(); // 시작 상태 = Neutral
        }

        GameObject FindChild(string childName)
        {
            var t = highlightsRoot.Find(childName);
            return t != null ? t.gameObject : null;
        }

        // 정확히 하나만 활성: 호버 대상이 있으면 그것, 없으면 Neutral.
        void Refresh()
        {
            var show = _current != null ? _current : _neutral;
            foreach (var go in _children)
                if (go != null) go.SetActive(go == show);
        }
    }

    /// <summary>버튼 하나의 포인터 enter/exit를 콜백으로 포워딩(런타임에 AddComponent). 네이티브 Button·
    /// <see cref="ButtonSpriteSwap"/>의 핸들러와 공존한다(EventSystem은 오브젝트의 모든 핸들러에 디스패치).</summary>
    public class TitleHighlightHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Action _onEnter, _onExit;
        public void Init(Action onEnter, Action onExit) { _onEnter = onEnter; _onExit = onExit; }
        public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => _onExit?.Invoke();
    }
}
