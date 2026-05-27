using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Stage
{
    /// <summary>
    /// 카메라/스크린FX 연출 프리셋 모음 (Phase D5).
    /// CSV에서 `FX,CamPreset:이름` 한 줄로 호출 → 이 SO에 등록된 step 시퀀스가 순차 실행.
    ///
    /// 사용:
    ///   1) Project 우클릭 → Create → LoveAlgo/Camera Preset Library
    ///   2) Resources/Data/CameraPresets.asset 으로 저장 (이름 고정)
    ///   3) entries에 프리셋 추가, 각 프리셋의 steps에 FX 명령 나열
    ///   4) CSV: ,FX,,CamPreset:이름,await
    ///
    /// SO 없거나 이름 미등록 시 CameraPresetTable의 내장 폴백 프리셋이 동작 — 역호환 + 즉시-가용.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraPresets", menuName = "LoveAlgo/Camera Preset Library")]
    public class CameraPresetSO : ScriptableObject
    {
        [SerializeField] List<Entry> entries = new();
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>한 프리셋 = 이름 + 순차 step 목록.</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("프리셋 이름. CSV에서 CamPreset:이름 으로 호출. 소문자 무시 비교.")]
            public string id;

            [Tooltip("기획자 메모 — 코드 무시.")]
            public string notes;

            [Tooltip("순차 실행되는 FX step들. 위에서 아래로.")]
            public List<Step> steps = new();
        }

        /// <summary>
        /// 한 step = ScreenFX에 그대로 넘길 FX 명령 문자열 + 부가 옵션.
        /// command 형식: PascalCase canonical ("CamShake:0.3:strong", "CamZoom:1.2:0.4" 등).
        /// </summary>
        [Serializable]
        public class Step
        {
            [Tooltip("ScreenFX 명령. 예: 'CamShake:0.3:strong', 'CamZoom:1.2:0.4', 'Flash'")]
            public string command;

            [Tooltip("이 step 실행 전 대기 시간(초). 0이면 즉시.")]
            public float delaySec;

            [Tooltip("step 완료까지 await 할지. false면 fire-and-forget으로 다음 step 시작.")]
            public bool waitForCompletion = true;
        }
    }
}
