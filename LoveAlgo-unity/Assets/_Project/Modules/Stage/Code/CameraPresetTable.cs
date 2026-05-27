using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Stage
{
    /// <summary>
    /// 카메라 프리셋 조회 테이블 (Phase D5).
    /// Resources/Data/CameraPresets.asset (CameraPresetSO) 우선 사용, 없으면 내장 기본 프리셋 폴백.
    ///
    /// 호출 흐름:
    ///   FXLineExecutor → CameraPresetRunner.RunAsync(name) → CameraPresetTable.Resolve(name) → SO entry
    /// </summary>
    public static class CameraPresetTable
    {
        const string ResourcesPath = "Data/CameraPresets";

        static readonly Dictionary<string, CameraPresetSO.Entry> _byId
            = new(StringComparer.OrdinalIgnoreCase);
        static bool _loaded;

        static CameraPresetTable() { Load(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload()
        {
            _byId.Clear();
            _loaded = false;
            Load();
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            // 1) 사용자 SO 우선
            var so = Resources.Load<CameraPresetSO>(ResourcesPath);
            if (so != null)
            {
                foreach (var entry in so.Entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
                    _byId[entry.id.Trim()] = entry;
                }
                Debug.Log($"[CameraPresetTable] CameraPresets.asset에서 {_byId.Count}개 프리셋 로드");
            }

            // 2) 내장 폴백 — SO에서 같은 id를 안 덮어쓴 경우만 보충
            foreach (var fb in BuildFallbackPresets())
            {
                if (!_byId.ContainsKey(fb.id))
                    _byId[fb.id] = fb;
            }
        }

        /// <summary>이름으로 프리셋 조회. 없으면 null.</summary>
        public static CameraPresetSO.Entry Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _byId.TryGetValue(name.Trim(), out var e) ? e : null;
        }

        /// <summary>등록된 프리셋 이름 일괄 조회 (디버그/미리보기 용).</summary>
        public static IEnumerable<string> Names => _byId.Keys;

        /// <summary>EditMode 테스트 격리용 — Resources 재로드.</summary>
        public static void ReloadForTests()
        {
            _byId.Clear();
            _loaded = false;
            Load();
        }

        /// <summary>
        /// 내장 폴백 프리셋들. SO 미작성 상태에서도 CSV가 즉시 동작하도록 6개 기본 제공.
        /// 기획자가 SO에 동일 id를 등록하면 그쪽이 우선 — 폴백은 안전망.
        /// </summary>
        static List<CameraPresetSO.Entry> BuildFallbackPresets()
        {
            var list = new List<CameraPresetSO.Entry>();

            list.Add(new CameraPresetSO.Entry
            {
                id = "ZoomIn-Soft",
                notes = "느린 줌인 — 대사 강조용",
                steps = { new CameraPresetSO.Step { command = "CamZoom:1.1:0.6" } }
            });

            list.Add(new CameraPresetSO.Entry
            {
                id = "ZoomIn-Hard",
                notes = "빠르고 강한 줌인 — 충격 컷",
                steps =
                {
                    new CameraPresetSO.Step { command = "CamZoom:1.25:0.18" },
                    new CameraPresetSO.Step { command = "CamShake:0.18:medium", delaySec = 0f },
                }
            });

            list.Add(new CameraPresetSO.Entry
            {
                id = "ZoomOut",
                notes = "원래 위치로 복귀",
                steps = { new CameraPresetSO.Step { command = "CamReset:0.5" } }
            });

            list.Add(new CameraPresetSO.Entry
            {
                id = "PunchHit",
                notes = "타격감 — 짧고 강한 흔들림 + 플래시",
                steps =
                {
                    new CameraPresetSO.Step { command = "Flash:0.12", waitForCompletion = false },
                    new CameraPresetSO.Step { command = "CamShake:0.25:strong" },
                }
            });

            list.Add(new CameraPresetSO.Entry
            {
                id = "DramaticReveal",
                notes = "큰 전환 — 페이드아웃 → 페이드인",
                steps =
                {
                    new CameraPresetSO.Step { command = "FadeOut:0.6" },
                    new CameraPresetSO.Step { command = "FadeIn:0.8" },
                }
            });

            list.Add(new CameraPresetSO.Entry
            {
                id = "Heartbeat",
                notes = "약한 반복 진동 — 긴장 분위기",
                steps =
                {
                    new CameraPresetSO.Step { command = "CamShake:0.15:weak" },
                    new CameraPresetSO.Step { command = "CamShake:0.15:weak", delaySec = 0.2f },
                }
            });

            return list;
        }
    }
}
