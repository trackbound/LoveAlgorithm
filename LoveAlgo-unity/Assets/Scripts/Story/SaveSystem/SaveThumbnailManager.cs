using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LoveAlgo.UI;

namespace LoveAlgo.Story.SaveSystem
{
    /// <summary>
    /// 썸네일 스크린샷 캡처, 생성, UI 숨김/복원 담당
    /// 화이트리스트 방식: "ThumbnailKeep" 태그가 붙은 GameObject(와 그 자손)는
    /// 캡처 시 그대로 유지되고, 그 외 UIManager/PopupManager 하위 UI는 모두 숨겨진다.
    /// 캐릭터 CG, 배경 BG는 UIManager/PopupManager 바깥에 있어 항상 유지됨.
    /// </summary>
    public static class SaveThumbnailManager
    {
        const string SaveFolder = "Saves";
        const int ThumbnailWidth = 400;
        const int ThumbnailHeight = 128;
        const string WhitelistTag = "ThumbnailKeep";

        struct ThumbnailUISnapshot
        {
            public List<GameObject> DisabledObjects;
        }

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
        /// 비동기 캡처: UI 숨김 → 1프레임 대기(렌더 반영) → 캡처 → UI 복원
        /// </summary>
        static async UniTask<Texture2D> CaptureStageOnlyTextureAsync()
        {
            var snapshot = HideUIForThumbnailCapture();
            try
            {
                await UniTask.WaitForEndOfFrame();
                return ScreenCapture.CaptureScreenshotAsTexture();
            }
            finally
            {
                RestoreUIAfterThumbnailCapture(snapshot);
            }
        }

        /// <summary>
        /// 동기 캡처 (폴백) — ForceUpdateCanvases 후 즉시 캡처
        /// </summary>
        static Texture2D CaptureStageOnlyTextureSync()
        {
            var snapshot = HideUIForThumbnailCapture();
            try
            {
                Canvas.ForceUpdateCanvases();
                return ScreenCapture.CaptureScreenshotAsTexture();
            }
            finally
            {
                RestoreUIAfterThumbnailCapture(snapshot);
            }
        }

        /// <summary>
        /// 썸네일 캡처를 위해 화이트리스트 외 UI 요소들 숨김.
        /// 유지(화이트리스트): 캐릭터 CG, 배경 BG(Stage 요소들 — UIManager/PopupManager 하위가 아님),
        ///                     ScheduleUI, ShopUI (UIManager 하위).
        /// 숨김: 그 외 UIManager 하위 UI 전부, PopupManager 하위 모든 팝업/레이어.
        /// </summary>
        static ThumbnailUISnapshot HideUIForThumbnailCapture()
        {
            var snapshot = new ThumbnailUISnapshot
            {
                DisabledObjects = new List<GameObject>()
            };

            var ui = UIManager.Instance;
            if (ui != null)
                HideActiveExceptWhitelist(ui.transform, snapshot.DisabledObjects);

            var popups = PopupManager.Instance;
            if (popups != null)
                HideActiveExceptWhitelist(popups.transform, snapshot.DisabledObjects);

            return snapshot;
        }

        /// <summary>
        /// root의 자손들을 순회하며, 화이트리스트에 해당하지 않는 활성 GameObject를 비활성화.
        /// 화이트리스트를 자손으로 가진 컨테이너는 비활성화하지 않고 재귀만 수행.
        /// </summary>
        static void HideActiveExceptWhitelist(Transform root, List<GameObject> disabledList)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var go = child.gameObject;

                if (IsWhitelisted(go))
                    continue; // 화이트리스트 — 서브트리 그대로 유지

                if (ContainsWhitelisted(child))
                {
                    // 자손 중 화이트리스트가 있으므로 컨테이너는 유지하고 내부만 처리
                    HideActiveExceptWhitelist(child, disabledList);
                }
                else if (go.activeSelf)
                {
                    go.SetActive(false);
                    disabledList.Add(go);
                }
            }
        }

        static bool IsWhitelisted(GameObject go)
        {
            return go.CompareTag(WhitelistTag);
        }

        static bool ContainsWhitelisted(Transform t)
        {
            // 비활성 자손까지 모두 검사 (프리팹 구조 상 아직 켜지지 않은 화이트리스트도 보호)
            var all = t.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].CompareTag(WhitelistTag)) return true;
            }
            return false;
        }

        /// <summary>
        /// 캡처 후 비활성화했던 GameObject들을 원복
        /// </summary>
        static void RestoreUIAfterThumbnailCapture(ThumbnailUISnapshot snapshot)
        {
            if (snapshot.DisabledObjects == null) return;
            for (int i = snapshot.DisabledObjects.Count - 1; i >= 0; i--)
            {
                var go = snapshot.DisabledObjects[i];
                if (go != null) go.SetActive(true);
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
