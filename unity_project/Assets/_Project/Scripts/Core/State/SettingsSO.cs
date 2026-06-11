using System;
using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 앱 설정 런타임 상태(볼륨·속도·해상도). <see cref="GameStateSO"/>와 같은 런타임-상태 SO 패턴 —
    /// 값은 <c>[NonSerialized]</c>라 에셋에 박히지 않고(에디터 오염 방지), 부팅 시 <c>SettingsStore</c>가
    /// PlayerPrefs에서 로드한다(세이브게임 JsonSaveStore와 분리된 앱 설정, 금지선6).
    ///
    /// <para>전역 읽기: <see cref="Shared"/>(Resources/Data/Settings) — <c>UiSoundSO.Shared</c> 선례.
    /// DialogueView(속도)·SettingsView/Controller가 읽고, 변경 통지/적용은 EventBus 커맨드(SettingsEvents)로.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Settings", menuName = "LoveAlgo/Settings")]
    public class SettingsSO : ScriptableObject
    {
        // 런타임 값(부팅 시 SettingsStore가 PlayerPrefs/기본값으로 로드). 에셋 직렬화 안 함.
        [NonSerialized] public float BgmVolume = 1f;
        [NonSerialized] public float SfxVolume = 1f;
        [NonSerialized] public float TextSpeed = 0.7f; // 0=느림~1=빠름(정규화)
        [NonSerialized] public float AutoSpeed = 0.5f; // 0=느림~1=빠름(정규화)
        [NonSerialized] public int ResolutionIndex = 4;
        [NonSerialized] public bool Fullscreen = true;

        static SettingsSO _shared;
        static bool _loaded;

        /// <summary>공유 인스턴스(Resources/Data/Settings). 부재 시 null — 소비처가 가드.</summary>
        public static SettingsSO Shared
        {
            get
            {
                if (!_loaded) { _shared = Resources.Load<SettingsSO>("Data/Settings"); _loaded = true; }
                return _shared;
            }
        }
    }
}
