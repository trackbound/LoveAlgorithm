using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Contracts;

namespace LoveAlgo.LockScreen.Data
{
    /// <summary>
    /// PC잠금 투두리스트 컨테이너 (33개 등록 권장).
    /// 첫 진입 시 PickRandom(3)로 표출.
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/LockScreen/ToDo List", fileName = "ToDoList")]
    public class ToDoListSO : ScriptableObject
    {
        [Tooltip("전체 ToDo 항목 (33개 권장)")]
        public List<ToDoItemSO> items = new List<ToDoItemSO>();

        /// <summary>
        /// 랜덤으로 n개 추출 (중복 없음). items가 n보다 적으면 가능한 만큼만 반환.
        /// </summary>
        public IReadOnlyList<ToDoItemSO> PickRandom(int n)
        {
            var result = new List<ToDoItemSO>();
            if (items == null || items.Count == 0 || n <= 0) return result;

            // Fisher-Yates shuffle (앞 n개만)
            var pool = new List<ToDoItemSO>(items);
            int take = Mathf.Min(n, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int j = Random.Range(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
                result.Add(pool[i]);
            }
            return result;
        }
    }
}
