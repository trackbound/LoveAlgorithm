using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 공통 표정 이름 상수
    /// 모든 캐릭터가 동일한 표정 세트를 Resources/Characters/{CharacterId}/ 폴더에 가짐
    /// </summary>
    public static class EmoteNames
    {
        public const string Default = "Default";
        public const string Happy = "Happy";       // 밝게 웃음
        public const string Laugh = "Laugh";       // 활짝 웃음
        public const string Smile = "Smile";       // 눈웃음
        public const string Sad = "Sad";           // 울먹
        public const string Shy = "Shy";           // 찌릿/부끄러움
        public const string Surprised = "Surprised"; // 놀람
        
        /// <summary>모든 표정 목록</summary>
        public static readonly string[] All = { Default, Happy, Laugh, Smile, Sad, Shy, Surprised };
    }

    /// <summary>
    /// 캐릭터 데이터 - 개별 캐릭터 정보
    /// 표정 스프라이트는 Resources.Load로 동적 로드 (SO에 등록 불필요)
    /// BGM/SFX는 CSV에서 명시적으로 재생
    /// </summary>
    [CreateAssetMenu(fileName = "Character_", menuName = "LoveAlgo/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("캐릭터 고유 ID (영문, 코드에서 사용)")]
        public string characterId = "";
        
        [Tooltip("표시 이름 (한글, UI에 표시)")]
        public string displayName = "";
        
        [Tooltip("기본 등장 슬롯 (L/C/R)")]
        [SerializeField] string defaultSlotString = "C";
        
        /// <summary>
        /// 기본 슬롯 (CharacterSlot.SlotPosition으로 변환)
        /// </summary>
        public SlotPosition DefaultSlot => defaultSlotString switch
        {
            "L" => SlotPosition.L,
            "R" => SlotPosition.R,
            _ => SlotPosition.C
        };

        [Header("스프라이트 (에디터 프리뷰용)")]
        [Tooltip("기본 표정 스프라이트 (에디터 썸네일용, 런타임에서는 Resources.Load 사용)")]
        public Sprite defaultSprite;

        [Header("스프라이트 트랜스폼")]
        [Tooltip("스케일 배율 (1 = 기본)")]
        public float spriteScale = 1f;
        
        [Tooltip("X 오프셋 (좌우 조정)")]
        public float offsetX = 0f;
        
        [Tooltip("Y 오프셋 (상하 조정, 발 위치 맞춤용)")]
        public float offsetY = 0f;
        
        [Tooltip("피벗 Y (0=하단, 0.5=중앙, 1=상단)")]
        [Range(0f, 1f)]
        public float pivotY = 0f;

        [Header("호감도 이벤트")]
        [Tooltip("호감도 도달 시 트리거되는 이벤트")]
        public List<LoveEvent> loveEvents = new();

        [Header("스토리 플래그")]
        [Tooltip("이 캐릭터 루트 진입에 필요한 플래그")]
        public List<string> routeRequiredFlags = new();
        
        [Tooltip("이 캐릭터 루트 진입에 필요한 최소 호감도")]
        public int routeRequiredLove = 0;

        /// <summary>
        /// 표정 스프라이트 로드 (Resources에서 동적 로드)
        /// </summary>
        public Sprite LoadEmoteSprite(string emoteName)
        {
            if (string.IsNullOrEmpty(emoteName))
                emoteName = EmoteNames.Default;
            
            string path = $"Characters/{characterId}/{emoteName}";
            var sprite = Resources.Load<Sprite>(path);
            
            // Default로 폴백
            if (sprite == null && emoteName != EmoteNames.Default)
            {
                path = $"Characters/{characterId}/{EmoteNames.Default}";
                sprite = Resources.Load<Sprite>(path);
            }
            
            return sprite ?? defaultSprite;
        }

        /// <summary>
        /// 캐릭터 트랜스폼 값 가져오기
        /// </summary>
        public void GetTransform(out float scale, out float oX, out float oY, out float pY)
        {
            scale = spriteScale;
            oX = offsetX;
            oY = offsetY;
            pY = pivotY;
        }

        /// <summary>
        /// 호감도에 해당하는 이벤트 가져오기
        /// </summary>
        public LoveEvent GetLoveEventAt(int lovePoints)
        {
            return loveEvents.Find(e => e.requiredLove == lovePoints);
        }

        /// <summary>
        /// 호감도 범위 내 이벤트들 가져오기
        /// </summary>
        public List<LoveEvent> GetLoveEventsInRange(int fromLove, int toLove)
        {
            return loveEvents.FindAll(e => e.requiredLove > fromLove && e.requiredLove <= toLove);
        }
    }

    /// <summary>
    /// 호감도 이벤트
    /// </summary>
    [Serializable]
    public class LoveEvent
    {
        [Tooltip("이벤트 트리거 호감도")]
        public int requiredLove;
        
        [Tooltip("이벤트 설명 (에디터용)")]
        public string description;
        
        [Tooltip("트리거할 스크립트 이름 (CSV 파일명)")]
        public string scriptName;
        
        [Tooltip("설정할 플래그")]
        public string flagToSet;
    }
}
