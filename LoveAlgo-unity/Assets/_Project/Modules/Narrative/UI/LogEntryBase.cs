using UnityEngine;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로그 엔트리 베이스 — 버블 복제 담당.
    /// 헤더(이름/초상화)는 프리팹 내부에 직접 배치되므로 런타임 헤더 생성 없음.
    /// </summary>
    public abstract class LogEntryBase : MonoBehaviour
    {
        [Header("대사 버블")]
        [SerializeField] protected RectTransform dialogueColumn;
        [SerializeField] protected GameObject bubbleTemplate;

        protected virtual void Awake()
        {
            // 프리팹에 미리 배치된 템플릿 인스턴스를 비활성화하여 고스트 버블 방지
            if (dialogueColumn == null) return;
            for (int i = 0; i < dialogueColumn.childCount; i++)
                dialogueColumn.GetChild(i).gameObject.SetActive(false);
        }

        public abstract void Init(string speaker, Sprite portrait, bool isUser);

        /// <summary>대사 버블 1개 추가</summary>
        public virtual void AddLine(string text)
        {
            var template = ResolveBubbleTemplate();
            if (template == null || dialogueColumn == null) return;

            var go = Instantiate(template, dialogueColumn);
            go.SetActive(true);

            var tmp = go.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = text;
        }

        /// <summary>서브클래스가 모드별 템플릿 교체 가능</summary>
        protected virtual GameObject ResolveBubbleTemplate() => bubbleTemplate;
    }
}
