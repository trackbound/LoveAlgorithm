using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 시각 표현 DB — 트랜스폼만 담당.
    /// 씬의 StageManager에 SerializeField로 바인딩 (Resources 의존 없음).
    /// VirtualOverlay(가상 캐릭터, 로아 등)는 StoryMappings.Overlays 분리.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStageDatabase", menuName = "LoveAlgo/Character Stage Database")]
    public class CharacterStageDatabase : ScriptableObject
    {
        [Header("캐릭터 표현 목록")]
        public List<CharacterStageEntry> entries = new();

        public CharacterStageEntry GetById(string characterId) =>
            entries.Find(e => e.characterId.Equals(characterId, StringComparison.OrdinalIgnoreCase));
    }

    [Serializable]
    public class CharacterStageEntry
    {
        [Tooltip("캐릭터 ID (StoryMappings.Characters와 동일)")]
        public string characterId = "";

        [Header("스프라이트 트랜스폼")]
        [Tooltip("스케일 배율 (1 = 기본)")]
        public float spriteScale = 1f;

        [Tooltip("X 오프셋")]
        public float offsetX = 0f;

        [Tooltip("Y 오프셋")]
        public float offsetY = 0f;

        [Tooltip("피벗 Y (0=하단, 0.5=중앙, 1=상단)")]
        [Range(0f, 1f)]
        public float pivotY = 0f;

        public void GetTransform(out float scale, out float oX, out float oY, out float pY)
        {
            scale = spriteScale;
            oX = offsetX;
            oY = offsetY;
            pY = pivotY;
        }
    }
}
