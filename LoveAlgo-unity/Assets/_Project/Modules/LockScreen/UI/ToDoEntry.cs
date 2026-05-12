using LoveAlgo.LockScreen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// ToDoWidget 내부 단일 항목 (체크박스 + 텍스트).
    /// </summary>
    public class ToDoEntry : MonoBehaviour
    {
        [SerializeField] Toggle checkbox;
        [SerializeField] TMP_Text label;

        public void Bind(ToDoItemSO item)
        {
            if (item == null) return;
            if (checkbox != null) checkbox.SetIsOnWithoutNotify(item.defaultChecked);
            if (label != null) label.text = item.text;
        }
    }
}
