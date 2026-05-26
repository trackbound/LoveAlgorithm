using LoveAlgo.Contracts;
using System.Collections.Generic;
using LoveAlgo.LockScreen.Data;
using UnityEngine;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// 잠금화면 우측 ToDo 위젯. 3개 항목 표시.
    /// ILockScreen.GetRandomToDos(3) 호출 → ToDoEntry 슬롯에 바인딩.
    /// </summary>
    public class ToDoWidget : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("ToDoEntry 슬롯들 (기본 3개)")]
        [SerializeField] List<ToDoEntry> entrySlots = new List<ToDoEntry>();

        [Header("Settings")]
        [SerializeField] int pickCount = 3;

        public void Populate(ILockScreen lockScreen)
        {
            if (lockScreen == null || entrySlots == null) return;

            var picks = lockScreen.GetRandomToDos(pickCount);
            for (int i = 0; i < entrySlots.Count; i++)
            {
                var slot = entrySlots[i];
                if (slot == null) continue;

                if (i < picks.Count)
                {
                    slot.gameObject.SetActive(true);
                    slot.Bind(picks[i]);
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }
    }
}
