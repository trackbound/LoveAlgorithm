using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 오디오 설정 ScriptableObject - 캐릭터별 BGM 매핑 등
    /// 에디터 툴(AudioManagerWindow)에서 편집, 런타임(AudioManager)에서 참조
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "LoveAlgo/Audio Settings")]
    public class AudioSettings : ScriptableObject
    {
        [Header("기본 설정")]
        [Tooltip("기본 BGM (캐릭터 없을 때)")]
        public AudioClip defaultBGM;
        
        [Tooltip("BGM 페이드 인/아웃 시간 (초)")]
        public float bgmFadeDuration = 4f;
        
        [Tooltip("캐릭터 등장 시 자동으로 BGM 전환 (비권장 - CSV에서 명시적 지정 권장)")]
        public bool autoSwitchOnCharacterEnter = false;
        
        [Tooltip("BGM 간 크로스페이드 사용")]
        public bool useCrossfade = true;

        [Header("캐릭터별 BGM")]
        public List<CharacterBGMMapping> characterBGMs = new();

        [Header("볼륨 프리셋")]
        public float defaultBGMVolume = 0.7f;
        public float defaultSFXVolume = 1f;
        public float defaultVoiceVolume = 1f;

        /// <summary>
        /// 캐릭터 이름으로 BGM 클립 가져오기
        /// </summary>
        public AudioClip GetCharacterBGM(string characterName)
        {
            var mapping = characterBGMs.Find(m => 
                m.characterName.Equals(characterName, StringComparison.OrdinalIgnoreCase));
            return mapping?.bgmClip;
        }

        /// <summary>
        /// 캐릭터 이름으로 BGM 리소스 경로 가져오기
        /// </summary>
        public string GetCharacterBGMPath(string characterName)
        {
            var mapping = characterBGMs.Find(m => 
                m.characterName.Equals(characterName, StringComparison.OrdinalIgnoreCase));
            return mapping?.bgmResourcePath;
        }
    }

    [Serializable]
    public class CharacterBGMMapping
    {
        [Tooltip("캐릭터 이름 (CSV에서 사용하는 이름)")]
        public string characterName;
        
        [Tooltip("캐릭터 전용 BGM 클립")]
        public AudioClip bgmClip;
        
        [Tooltip("Resources 경로 (Audio/BGM/ 이후)")]
        public string bgmResourcePath;
        
        [Tooltip("이 캐릭터 BGM 우선순위 (높을수록 우선)")]
        public int priority = 0;
    }
}
