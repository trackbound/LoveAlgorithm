using System.Diagnostics;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;
using LoveAlgo.Modules.Audio;
using LoveAlgo.Stage;
using LoveAlgo.Story;

namespace LoveAlgo.Story.SaveSystem
{
    /// <summary>
    /// SaveData의 무대 상태(배경/캐릭터/BGM/CG/SD/Overlay/딤/FX)를 즉시(Cut, 0초) 복원.
    /// 세이브 로드와 디버그 점프 양쪽에서 공유하는 단일 복원 경로.
    ///
    /// verbose 모드 (PlayerPrefs "StageSync.Verbose"): 항목별 시간 측정 로그.
    /// </summary>
    public static class StageRestorer
    {
        const string Tag = "StageRestore";

        public static async UniTask RestoreAsync(SaveData data)
        {
            if (data == null) return;

            var total = Stopwatch.StartNew();
            int applied = 0;

            // 배경 복원
            if (!string.IsNullOrEmpty(data.CurrentBG))
            {
                var bg = StageModule.Instance?.Background;
                if (bg != null)
                {
                    var sw = Stopwatch.StartNew();
                    await bg.ChangeBackgroundAsync(data.CurrentBG, BGTransition.Cut, 0f);
                    sw.Stop();
                    applied++;
                    StageSyncLog.Detail(Tag, $"BG: {data.CurrentBG} ({sw.Elapsed.TotalMilliseconds:0.0}ms)");
                }
            }

            // 캐릭터 복원
            if (data.Characters != null && data.Characters.Count > 0)
            {
                var charLayer = StageModule.Instance?.Character;
                if (charLayer != null)
                {
                    foreach (var charInfo in data.Characters)
                    {
                        SlotPosition pos;
                        switch (charInfo.Slot)
                        {
                            case "L": pos = SlotPosition.L; break;
                            case "R": pos = SlotPosition.R; break;
                            default:  pos = SlotPosition.C; break;
                        }
                        var slot = charLayer.GetSlot(pos);
                        if (slot != null)
                        {
                            // 오버레이 캐릭터(로아 등) 복원 시 시그니처 SFX (정상 Enter와 동일)
                            if (StoryMappings.GetOverlay(charInfo.Character) != null)
                                AudioManager.Instance?.PlayCharacterEntrySFX(charInfo.Character);

                            var sw = Stopwatch.StartNew();
                            await slot.EnterAsync(charInfo.Character, charInfo.Emote);
                            sw.Stop();
                            applied++;
                            StageSyncLog.Detail(Tag,
                                $"Char {charInfo.Slot}: {charInfo.Character}/{charInfo.Emote} ({sw.Elapsed.TotalMilliseconds:0.0}ms)");
                        }
                    }
                }
            }

            // CG 복원
            if (!string.IsNullOrEmpty(data.CurrentCG))
            {
                var cg = StageModule.Instance?.CG;
                if (cg != null)
                {
                    var sw = Stopwatch.StartNew();
                    await cg.ShowAsync(data.CurrentCG, 0f);
                    sw.Stop();
                    applied++;
                    StageSyncLog.Detail(Tag, $"CG: {data.CurrentCG} ({sw.Elapsed.TotalMilliseconds:0.0}ms)");
                }
            }

            // SD 컷씬 복원
            if (!string.IsNullOrEmpty(data.CurrentSD))
            {
                var sd = StageModule.Instance?.SDCutscene;
                if (sd != null)
                {
                    StageModule.Instance?.Character?.SetVisibleImmediate(false);
                    var sw = Stopwatch.StartNew();
                    await sd.ShowAsync(data.CurrentSD, 0f);
                    sw.Stop();
                    applied++;
                    StageSyncLog.Detail(Tag, $"SD: {data.CurrentSD} ({sw.Elapsed.TotalMilliseconds:0.0}ms)");
                }
            }

            // VirtualBG 오버레이 복원
            if (!string.IsNullOrEmpty(data.CurrentOverlay))
            {
                var overlay = StageModule.Instance?.VirtualBG;
                if (overlay != null)
                {
                    var sw = Stopwatch.StartNew();
                    await overlay.ShowAsync(data.CurrentOverlay, 0f);
                    sw.Stop();
                    applied++;
                    StageSyncLog.Detail(Tag, $"Overlay: {data.CurrentOverlay} ({sw.Elapsed.TotalMilliseconds:0.0}ms)");
                }
            }

            // 독백 딤 복원
            if (data.IsMonologueDimShowing)
            {
                StageModule.Instance?.MonologueDim?.ShowImmediate();
                applied++;
                StageSyncLog.Detail(Tag, "MonologueDim: show");
            }

            // 화면 효과 복원
            var fx = ScreenFX.Instance;
            if (fx != null)
            {
                if (data.IsEyeClosed)      { fx.EyeCloseImmediate(); StageSyncLog.Detail(Tag, "FX: EyeClose"); applied++; }
                else if (data.IsFadeBlack) { fx.SetBlack();          StageSyncLog.Detail(Tag, "FX: FadeBlack"); applied++; }
                else                         fx.SetClear();
            }

            // BGM 복원
            if (!string.IsNullOrEmpty(data.CurrentBGM) && AudioManager.Instance != null)
            {
                var sw = Stopwatch.StartNew();
                await AudioManager.Instance.PlayBGMAsync(data.CurrentBGM, 0.5f);
                sw.Stop();
                applied++;
                StageSyncLog.Detail(Tag, $"BGM: {data.CurrentBGM} ({sw.Elapsed.TotalMilliseconds:0.0}ms)");
            }

            total.Stop();
            StageSyncLog.Info(Tag, $"Applied {applied} items in {total.Elapsed.TotalMilliseconds:0.0}ms");
        }
    }
}
