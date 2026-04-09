using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로그 엔트리 베이스 — 버블 복제만 담당
    /// 각 서브클래스 프리팹에 헤더(이름/초상화)가 이미 배치되어 있으므로
    /// 런타임 헤더 생성 없이 SerializeField 직접 바인딩으로 처리
    /// </summary>
    public abstract class LogEntryBase : MonoBehaviour
    {
        [Header("대사 버블")]
        [SerializeField] protected RectTransform dialogueColumn;
        [SerializeField] protected GameObject bubbleTemplate;

        /// <summary>
        /// Awake — Instantiate 즉시 실행.
        /// dialogueColumn 안의 프리팹 템플릿 인스턴스를 비활성화하여 고스트 버블 방지.
        /// (bubbleTemplate은 외부 프리팹 에셋 참조이므로 SetActive 무효)
        /// </summary>
        protected virtual void Awake()
        {
            if (dialogueColumn == null) return;
            for (int i = 0; i < dialogueColumn.childCount; i++)
                dialogueColumn.GetChild(i).gameObject.SetActive(false);
        }

        /// <summary>
        /// 초기화 — Instantiate 직후 호출.
        /// 서브클래스에서 이름/초상화 등을 설정한다.
        /// </summary>
        public abstract void Init(string speaker, Sprite portrait);

        /// <summary>대사 버블 1개 추가</summary>
        public virtual void AddLine(string text)
        {
            if (bubbleTemplate == null || dialogueColumn == null) return;

            var go = Instantiate(bubbleTemplate, dialogueColumn);
            go.SetActive(true);

            var tmp = go.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                tmp.text = text;
        }
    }
}
