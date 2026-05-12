using UnityEngine;

namespace LoveAlgo.LockScreen.Data
{
    /// <summary>
    /// PC잠금 화면 투두리스트 단일 항목.
    /// 33개를 ToDoListSO에 등록 후 랜덤 3개 추출.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/LockScreen/ToDo Item", fileName = "ToDo_")]
    public class ToDoItemSO : ScriptableObject
    {
        [Tooltip("유니크 ID (저장/추적용)")]
        public string id;

        [Tooltip("표시 텍스트")]
        [TextArea(1, 3)]
        public string text;

        [Tooltip("기본 체크 상태")]
        public bool defaultChecked;
    }
}
