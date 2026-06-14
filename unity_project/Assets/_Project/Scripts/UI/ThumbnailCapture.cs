using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 세이브 썸네일 캡처(순수 헬퍼+코루틴). UI 캔버스(sortingOrder≥0)를 일시 비활성 → 프레임 종료까지 대기 →
    /// 백버퍼를 읽어(스테이지만 남음) 다운스케일 → PNG로 기록(HANDOFF "UI 레이어 배제 캡처" 충족, 월드 카메라 신설 회피).
    /// VN이라 월드 카메라가 없어 화면 캡처 후 캔버스만 토글하는 옵션 A를 택함. yield는 최상위에만 두고
    /// 읽기/복원/인코딩/IO는 비-이터레이터 헬퍼에 모아 try/catch/finally를 안전하게 사용한다.
    /// </summary>
    public static class ThumbnailCapture
    {
        public const int Width = 400;
        public const int Height = 128;

        /// <summary>캔버스 배제 후 화면을 캡처해 PNG 파일로 저장한다(코루틴: WaitForEndOfFrame 필요).</summary>
        public static IEnumerator CaptureToFile(string filePath, int width = Width, int height = Height)
        {
            var disabled = DisableForegroundCanvases();
            yield return new WaitForEndOfFrame(); // 렌더 종료 후라야 백버퍼 ReadPixels 가능
            CaptureAndWrite(filePath, width, height, disabled);
        }

        static List<Canvas> DisableForegroundCanvases()
        {
            var disabled = new List<Canvas>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>())
                if (c != null && c.enabled && c.sortingOrder >= 0) { c.enabled = false; disabled.Add(c); }
            return disabled;
        }

        static void CaptureAndWrite(string filePath, int width, int height, List<Canvas> toRestore)
        {
            Texture2D full = null, scaled = null;
            try
            {
                int sw = Screen.width, sh = Screen.height;
                full = new Texture2D(sw, sh, TextureFormat.RGB24, false);
                full.ReadPixels(new Rect(0, 0, sw, sh), 0, 0);
                full.Apply();
                scaled = Downscale(full, width, height);
                byte[] png = scaled.EncodeToPNG();
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllBytes(filePath, png);
            }
            catch (Exception e) { Debug.LogError($"[ThumbnailCapture] 캡처/저장 실패 {filePath}: {e}"); }
            finally
            {
                foreach (var c in toRestore) if (c != null) c.enabled = true; // 예외에도 복원
                if (full != null) UnityEngine.Object.Destroy(full);
                if (scaled != null) UnityEngine.Object.Destroy(scaled);
            }
        }

        static Texture2D Downscale(Texture2D src, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var dst = new Texture2D(width, height, TextureFormat.RGB24, false);
                dst.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                dst.Apply();
                return dst;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
