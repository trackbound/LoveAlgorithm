using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// CG 레이어 - 배경 위에 오버레이로 표시되는 CG 이미지 관리
    /// CG 표시 시 캐릭터 자동 퇴장 + 대사창 자동 숨김
    /// </summary>
    public class CGLayer : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Image cgImage;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("설정")]
        [SerializeField] float defaultDuration = 1f;

        string currentCG;
        bool isShowing;

        /// <summary>
        /// 현재 표시 중인 CG
        /// </summary>
        public string CurrentCG => currentCG;

        /// <summary>
        /// CG 표시 중 여부
        /// </summary>
        public bool IsShowing => isShowing;

        void Awake()
        {
            // 초기 상태: 숨김
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (cgImage != null)
            {
                cgImage.enabled = false;
            }
        }

        /// <summary>
        /// CG 명령 실행
        /// Value 형식: CG이름[:전환타입:시간] 또는 Exit[:시간]
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            string command = parts[0];

            // Exit 명령
            if (command.Equals("Exit", System.StringComparison.OrdinalIgnoreCase))
            {
                float duration = defaultDuration;
                if (parts.Length >= 2 && float.TryParse(parts[1], out float d))
                {
                    duration = d;
                }
                await HideAsync(duration, ct);
                return;
            }

            // CG 표시
            string cgName = command;
            float showDuration = defaultDuration;
            if (parts.Length >= 3 && float.TryParse(parts[2], out float parsedDuration))
            {
                showDuration = parsedDuration;
            }

            await ShowAsync(cgName, showDuration, ct);
        }

        /// <summary>
        /// CG 표시 (페이드인)
        /// </summary>
        public async UniTask ShowAsync(string cgName, float duration = 1f, CancellationToken ct = default)
        {
            // 스프라이트 로드
            var sprite = LoadSprite(cgName);
            if (sprite == null)
            {
                Debug.LogWarning($"[CGLayer] CG 스프라이트 없음: {cgName}");
                return;
            }

            currentCG = cgName;
            isShowing = true;

            // 이미지 설정
            cgImage.sprite = sprite;
            cgImage.enabled = true;
            cgImage.preserveAspect = true;

            // 페이드인
            if (canvasGroup != null && duration > 0)
            {
                canvasGroup.alpha = 0f;
                await canvasGroup.DOFade(1f, duration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            else if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            Debug.Log($"[CGLayer] CG 표시: {cgName}");
        }

        /// <summary>
        /// CG 숨기기 (페이드아웃)
        /// </summary>
        public async UniTask HideAsync(float duration = 1f, CancellationToken ct = default)
        {
            if (!isShowing) return;

            // 페이드아웃
            if (canvasGroup != null && duration > 0)
            {
                await canvasGroup.DOFade(0f, duration)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            else if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            cgImage.enabled = false;
            cgImage.sprite = null;
            currentCG = null;
            isShowing = false;

            Debug.Log("[CGLayer] CG 숨김");
        }

        /// <summary>
        /// 즉시 클리어 (애니메이션 없이)
        /// </summary>
        public void Clear()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (cgImage != null)
            {
                cgImage.enabled = false;
                cgImage.sprite = null;
            }
            currentCG = null;
            isShowing = false;
        }

        /// <summary>
        /// 스프라이트 로드
        /// </summary>
        Sprite LoadSprite(string cgName)
        {
            // Resources/CG/폴더/파일 형식으로 로드
            // 예: CG/Roa_FirstMeet → Resources/CG/Roa_FirstMeet
            var sprite = Resources.Load<Sprite>($"CG/{cgName}");
            
            if (sprite == null)
            {
                // 폴백: 직접 경로
                sprite = Resources.Load<Sprite>(cgName);
            }

            return sprite;
        }
    }
}
