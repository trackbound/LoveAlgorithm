using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;

namespace LoveAlgo.Story.SaveSystem
{
    /// <summary>
    /// 썸네일 스크린샷 캡처, 생성, UI 숨김/복원 담당
    /// </summary>
    public static class SaveThumbnailManager
    {
        const string SaveFolder = "Saves";
        const int ThumbnailWidth = 400;
        const int ThumbnailHeight = 128;

        /// <summary>
        /// 스크린샷 저장 경로
        /// </summary>
        public static string GetScreenshotPath(int slot)
        {
            string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"save_{slot:D2}_thumb.png");
        }

        /// <summary>
        /// 팝업 열기 전 게임 화면을 임시 파일로 미리 캡처 (비동기)
        /// UI를 숨기고 1프레임 대기 후 캡처 → UI/팝업이 썸네일에 포함되지 않음
        /// </summary>
        public static async UniTask CapturePendingScreenshotAsync()
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var tex = await CaptureStageOnlyTextureAsync();
                var thumb = CropAndScaleTexture(tex, ThumbnailWidth, ThumbnailHeight);
                File.WriteAllBytes(
                    Path.Combine(folder, "save_pending_thumb.png"),
                    thumb.EncodeToPNG()
                );
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(thumb);
                Debug.Log("[SaveThumbnailManager] 팬딩 스크린샷 캡처 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 팬딩 스크린샷 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 동기 버전 (하위 호환 — 자동저장 등에서 사용)
        /// 같은 프레임 캡처이므로 UI가 찍힐 수 있음. 가능하면 Async 버전 사용
        /// </summary>
        public static void CapturePendingScreenshot()
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var tex = CaptureStageOnlyTextureSync();
                var thumb = CropAndScaleTexture(tex, ThumbnailWidth, ThumbnailHeight);
                File.WriteAllBytes(
                    Path.Combine(folder, "save_pending_thumb.png"),
                    thumb.EncodeToPNG()
                );
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(thumb);
                Debug.Log("[SaveThumbnailManager] 팬딩 스크린샷 캡처 완료 (sync)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 팬딩 스크린샷 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 임시 파일을 실제 슬롯 썸네일로 확정
        /// 성공 시 true, 없으면 false
        /// </summary>
        public static bool TryCommitPendingScreenshot(int slot)
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, SaveFolder);
                string pending = Path.Combine(folder, "save_pending_thumb.png");
                if (File.Exists(pending))
                {
                    File.Copy(pending, GetScreenshotPath(slot), overwrite: true);
                    File.Delete(pending);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 썸네일 확정 실패: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// 스크린샷 직접 캡처 및 저장 (자동저장 등 팝업 없는 경우)
        /// </summary>
        public static void CaptureScreenshot(int slot)
        {
            try
            {
                var tex = CaptureStageOnlyTextureSync();
                var thumb = CropAndScaleTexture(tex, ThumbnailWidth, ThumbnailHeight);
                byte[] png = thumb.EncodeToPNG();
                File.WriteAllBytes(GetScreenshotPath(slot), png);

                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(thumb);
                Debug.Log($"[SaveThumbnailManager] 슬롯 {slot} 스크린샷 저장");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 스크린샷 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 스크린샷 로드 (슬롯 UI용)
        /// </summary>
        public static Sprite LoadScreenshot(int slot)
        {
            string path = GetScreenshotPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                byte[] png = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(png);
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveThumbnailManager] 스크린샷 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 스크린샷 삭제
        /// </summary>
        public static void DeleteScreenshot(int slot)
        {
            string path = GetScreenshotPath(slot);
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// 비동기 캡처: Stage 외 Canvas 비활성화 → 1프레임 대기(렌더 반영) → 캡처 → 복원
        /// </summary>
        static async UniTask<Texture2D> CaptureStageOnlyTextureAsync()
        {
            var disabled = DisableNonStageCanvases();
            try
            {
                await UniTask.WaitForEndOfFrame();
                return ScreenCapture.CaptureScreenshotAsTexture();
            }
            finally
            {
                RestoreCanvases(disabled);
            }
        }

        /// <summary>
        /// 동기 캡처 (폴백) — ForceUpdateCanvases 후 즉시 캡처
        /// </summary>
        static Texture2D CaptureStageOnlyTextureSync()
        {
            var disabled = DisableNonStageCanvases();
            try
            {
                Canvas.ForceUpdateCanvases();
                return ScreenCapture.CaptureScreenshotAsTexture();
            }
            finally
            {
                RestoreCanvases(disabled);
            }
        }

        /// <summary>
        /// Stage Canvas(게임 화면)를 제외한 모든 Canvas를 비활성화.
        /// 화이트리스트 방식 — 새 UI가 추가되어도 자동으로 썸네일에서 제외됨.
        /// </summary>
        static List<Canvas> DisableNonStageCanvases()
        {
            var stageCanvas = StageManager.Instance?.StageCanvas;
            var allCanvases = Canvas.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var disabled = new List<Canvas>();

            foreach (var canvas in allCanvases)
            {
                if (!canvas.enabled) continue;
                // Stage Canvas 자체는 유지
                if (canvas == stageCanvas) continue;
                // Stage Canvas 하위 Canvas(레이어 내부)도 유지
                if (stageCanvas != null && canvas.transform.IsChildOf(stageCanvas.transform)) continue;

                canvas.enabled = false;
                disabled.Add(canvas);
            }

            return disabled;
        }

        /// <summary>
        /// 비활성화했던 Canvas 복원
        /// </summary>
        static void RestoreCanvases(List<Canvas> disabled)
        {
            foreach (var canvas in disabled)
            {
                if (canvas != null)
                    canvas.enabled = true;
            }
        }

        /// <summary>
        /// 텍스쳐 중앙 크롭 후 축소
        /// </summary>
        static Texture2D CropAndScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var contentRect = DetectContentRect(source);

            float sourceAspect = (float)contentRect.width / contentRect.height;
            float targetAspect = (float)targetWidth / targetHeight;

            int cropWidth = contentRect.width;
            int cropHeight = contentRect.height;
            int cropX = contentRect.x;
            int cropY = contentRect.y;

            if (sourceAspect > targetAspect)
            {
                cropWidth = Mathf.Max(1, Mathf.RoundToInt(contentRect.height * targetAspect));
                cropX = contentRect.x + (contentRect.width - cropWidth) / 2;
            }
            else if (sourceAspect < targetAspect)
            {
                cropHeight = Mathf.Max(1, Mathf.RoundToInt(contentRect.width / targetAspect));
                cropY = contentRect.y + (contentRect.height - cropHeight) / 2;
            }

            cropWidth = Mathf.Max(1, cropWidth);
            cropHeight = Mathf.Max(1, cropHeight);

            var cropped = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false);
            cropped.SetPixels(source.GetPixels(cropX, cropY, cropWidth, cropHeight));
            cropped.Apply();

            var scaled = ScaleTexture(cropped, targetWidth, targetHeight);
            UnityEngine.Object.Destroy(cropped);
            return scaled;
        }

        /// <summary>
        /// 캡처 원본에서 레터박스/필러박스(검정 여백) 영역 탐지
        /// </summary>
        static RectInt DetectContentRect(Texture2D source)
        {
            int w = source.width;
            int h = source.height;
            if (w <= 0 || h <= 0)
                return new RectInt(0, 0, w, h);

            var pixels = source.GetPixels32();
            const int threshold = 10;
            const int step = 3;

            bool ColumnHasContent(int x)
            {
                for (int y = 0; y < h; y += step)
                {
                    var c = pixels[(y * w) + x];
                    if (c.a > threshold && (c.r > threshold || c.g > threshold || c.b > threshold))
                        return true;
                }
                return false;
            }

            bool RowHasContent(int y)
            {
                int baseIdx = y * w;
                for (int x = 0; x < w; x += step)
                {
                    var c = pixels[baseIdx + x];
                    if (c.a > threshold && (c.r > threshold || c.g > threshold || c.b > threshold))
                        return true;
                }
                return false;
            }

            int left = 0;
            while (left < w - 1 && !ColumnHasContent(left)) left++;

            int right = w - 1;
            while (right > left && !ColumnHasContent(right)) right--;

            int bottom = 0;
            while (bottom < h - 1 && !RowHasContent(bottom)) bottom++;

            int top = h - 1;
            while (top > bottom && !RowHasContent(top)) top--;

            int contentWidth = Mathf.Max(1, right - left + 1);
            int contentHeight = Mathf.Max(1, top - bottom + 1);
            return new RectInt(left, bottom, contentWidth, contentHeight);
        }

        /// <summary>
        /// 텍스처 리사이즈 (RenderTexture 기반 GPU 스케일링)
        /// </summary>
        static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                result.Apply();
                return result;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
