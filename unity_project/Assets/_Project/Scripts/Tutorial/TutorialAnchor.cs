using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 튜토리얼 앵커 — 하이라이트/강제 클릭 대상 UI에 부착하는 마커(id = 시퀀스 데이터의 앵커 id 계약:
    /// LeftPanel/RightPanel/InfoPanel/StatPanel/ActionArea/PartTimeTab/WorkStudyArea/
    /// ShopButton/ShopBalance/ShopCart/ShopItems/ShopBack). 씬 어디에 붙어도 정적 레지스트리로 조회되므로
    /// 튜토리얼 시스템은 씬 구조를 모른다 — 병렬 UI 재작업 합류 후 부착만 하면 연결.
    /// </summary>
    public class TutorialAnchor : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<TutorialAnchor> _all = new();

        [Tooltip("앵커 id(케이스 무시). 시퀀스 스텝의 highlightAnchor/requiredClickAnchor와 일치.")]
        [SerializeField] string id = "";
        [Tooltip("강제 클릭 패스스루 대상 버튼(선택). 비우면 같은 GO의 Button 자동 사용.")]
        [SerializeField] Button button;

        public string Id { get => id; set => id = value; }
        public Button Button { get => button; set => button = value; }
        public RectTransform Rect => transform as RectTransform;

        void OnEnable() => _all.Add(this);
        void OnDisable() => _all.Remove(this);

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
        }

        /// <summary>강제 클릭 패스스루 — 실제 버튼 동작 실행(기획: "한 번 눌러봐!" = 진짜 화면 전환까지).</summary>
        public void Invoke()
        {
            if (button != null) button.onClick.Invoke();
        }

        /// <summary>id로 활성 앵커 조회(케이스 무시). 없으면 null — 호출부 fail-open.</summary>
        public static TutorialAnchor Find(string anchorId)
        {
            if (string.IsNullOrEmpty(anchorId)) return null;
            for (int i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a != null && string.Equals(a.id, anchorId, System.StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }
    }
}
