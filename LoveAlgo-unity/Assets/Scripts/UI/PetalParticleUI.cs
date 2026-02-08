using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI Canvas 위에서 벚꽃잎이 바람에 하늘하늘 흩날리는 파티클 이펙트.
    /// - 3D 뒤집힘을 scaleX/scaleY 오실레이션으로 시뮬레이션
    /// - 2중 사인파 + 글로벌 윈드로 자연스러운 궤적
    /// - 낙하 속도 변속(공기저항 느낌)
    /// </summary>
    public class PetalParticleUI : MonoBehaviour
    {
        [Header("꽃잎 설정")]
        [SerializeField] Sprite petalSprite;
        [SerializeField] int maxPetals = 30;
        [SerializeField] float spawnInterval = 0.55f;

        [Header("크기")]
        [SerializeField] float minSize = 16f;
        [SerializeField] float maxSize = 36f;

        [Header("낙하")]
        [SerializeField] float minFallSpeed = 5f;
        [SerializeField] float maxFallSpeed = 14f;

        [Header("바람 (은은한 산들바람)")]
        [Tooltip("글로벌 바람 — 모든 꽃잎에 영향, 개별 감도로 다양성 확보")]
        [SerializeField] float windBaseX = 3f;            // 기본 횡방향(+ = 오른쪽)
        [SerializeField] float windGustStrength = 5f;     // 돌풍 강도
        [SerializeField] float windGustSpeed = 0.06f;     // 돌풍 주기 (Hz) — 아주 느리게
        [SerializeField] float windVertGust = 3f;         // 수직 돌풍 (위로 솟구침)

        [Header("개별 흔들림 (2중 사인파)")]
        [SerializeField] float minSwayAmp1 = 15f;
        [SerializeField] float maxSwayAmp1 = 35f;
        [SerializeField] float minSwayFreq1 = 0.08f;
        [SerializeField] float maxSwayFreq1 = 0.2f;
        [SerializeField] float minSwayAmp2 = 5f;
        [SerializeField] float maxSwayAmp2 = 12f;
        [SerializeField] float minSwayFreq2 = 0.25f;
        [SerializeField] float maxSwayFreq2 = 0.6f;

        [Header("3D 회전 시뮬레이션 (은은하게)")]
        [Tooltip("ScaleX/Y를 사인파로 줄였다 늘려 뒤집히는 듯한 효과")]
        [SerializeField] float minFlipSpeed = 0.06f;
        [SerializeField] float maxFlipSpeed = 0.2f;
        [SerializeField] float flipMinScale = 0.45f;     // 최소 scale — 너무 얇아지지 않게
        [SerializeField] float minTiltSpeed = 0.05f;
        [SerializeField] float maxTiltSpeed = 0.18f;

        [Header("Z 회전 (느린 기울기 변화)")]
        [SerializeField] float minZRotSpeed = 2f;
        [SerializeField] float maxZRotSpeed = 8f;

        [Header("투명도")]
        [SerializeField] float startAlpha = 0f;
        [SerializeField] float peakAlpha = 0.7f;
        [SerializeField] float fadeInRatio = 0.2f;
        [SerializeField] float fadeOutRatio = 0.3f;

        [Header("스폰 영역")]
        [SerializeField] float spawnMarginTop = 60f;
        [SerializeField] float spawnMarginSide = 120f;

        RectTransform rectTransform;
        readonly List<Petal> activePetals = new List<Petal>();
        float spawnTimer;
        int petalIndex;
        float globalTime;  // 글로벌 바람 시간

        class Petal
        {
            public RectTransform rect;
            public Image image;

            // 낙하
            public float fallSpeed;
            public float fallAccel;       // 미세 가감속

            // 흔들림 — 2중 사인파
            public float swayAmp1, swayFreq1, swayPhase1;
            public float swayAmp2, swayFreq2, swayPhase2;

            // 3D 뒤집힘
            public float flipSpeed, flipPhase;      // scaleX 오실레이션
            public float tiltSpeed, tiltPhase;      // scaleY 오실레이션

            // Z 회전 (느린)
            public float zRotSpeed;

            // 위치 추적
            public float posX, posY;
            public float baseX;       // 스폰 X (drift 기준)
            public float driftSpeed;  // 개별 횡이동
            public float windSensitivity; // 바람 감도 (0~1+) — 꽃잎마다 다르게

            // 수명
            public float lifetime, maxLifetime;
            public float alpha;
            public float size;
        }

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            spawnTimer = 0f;
        }

        void OnDisable()
        {
            foreach (var p in activePetals)
            {
                if (p.rect != null)
                    Destroy(p.rect.gameObject);
            }
            activePetals.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            globalTime += dt;

            // ── 글로벌 바람 (부드러운 산들바람) ──
            float windX = windBaseX
                + windGustStrength * Mathf.Sin(globalTime * windGustSpeed * Mathf.PI * 2f)
                + windGustStrength * 0.3f * Mathf.Sin(globalTime * windGustSpeed * 1.73f * Mathf.PI * 2f);
            float windY = windVertGust * Mathf.Sin(globalTime * windGustSpeed * 0.7f * Mathf.PI * 2f);

            // ── 스폰 ──
            spawnTimer += dt;
            if (spawnTimer >= spawnInterval && activePetals.Count < maxPetals)
            {
                spawnTimer = 0f;
                SpawnPetal();
            }

            // ── 꽃잎 업데이트 ──
            Rect area = rectTransform.rect;
            float bottomY = area.yMin - 80f;

            for (int i = activePetals.Count - 1; i >= 0; i--)
            {
                var p = activePetals[i];
                p.lifetime += dt;

                // 낙하 — 미세 가감속으로 공기 저항 느낌
                float currentFall = p.fallSpeed + p.fallAccel * Mathf.Sin(p.lifetime * 0.5f);
                // 수직 돌풍: 바람에 따라 살짝 떠오르기도 (개별 감도 반영)
                float vertOffset = windY * p.windSensitivity * (0.6f + 0.4f * Mathf.Sin(p.lifetime * 0.9f + p.swayPhase1));
                p.posY -= (currentFall - Mathf.Max(0f, vertOffset)) * dt;

                // 횡이동: 글로벌 바람(개별 감도) + 개별 drift + 2중 사인파 흔들림
                float sway1 = p.swayAmp1 * Mathf.Sin(p.swayFreq1 * p.lifetime * Mathf.PI * 2f + p.swayPhase1);
                float sway2 = p.swayAmp2 * Mathf.Sin(p.swayFreq2 * p.lifetime * Mathf.PI * 2f + p.swayPhase2);
                float xOffset = sway1 + sway2;

                p.posX = p.baseX + xOffset + (windX * p.windSensitivity + p.driftSpeed) * p.lifetime;

                p.rect.anchoredPosition = new Vector2(p.posX, p.posY);

                // ── 3D 회전 시뮬레이션 ──
                // scaleX: 사인파로 줄였다 늘림 → 좌우 뒤집히는 효과
                float flipT = Mathf.Sin(p.flipSpeed * p.lifetime * Mathf.PI * 2f + p.flipPhase);
                float scaleX = Mathf.Lerp(flipMinScale, 1f, (flipT + 1f) * 0.5f);

                // scaleY: 다른 주기로 위아래 압축 → 앞뒤 기울어지는 효과
                float tiltT = Mathf.Sin(p.tiltSpeed * p.lifetime * Mathf.PI * 2f + p.tiltPhase);
                float scaleY = Mathf.Lerp(flipMinScale + 0.2f, 1f, (tiltT + 1f) * 0.5f);

                p.rect.localScale = new Vector3(scaleX, scaleY, 1f);

                // Z 회전: 느리게 기울기가 변하는 정도
                float rot = p.rect.localEulerAngles.z + p.zRotSpeed * dt;
                p.rect.localEulerAngles = new Vector3(0f, 0f, rot);

                // ── 알파 ──
                float lifeRatio = p.lifetime / p.maxLifetime;
                if (lifeRatio < fadeInRatio)
                    p.alpha = Mathf.Lerp(startAlpha, peakAlpha, lifeRatio / fadeInRatio);
                else if (lifeRatio > (1f - fadeOutRatio))
                    p.alpha = Mathf.Lerp(peakAlpha, 0f, (lifeRatio - (1f - fadeOutRatio)) / fadeOutRatio);
                else
                    p.alpha = peakAlpha;

                var c = p.image.color;
                c.a = p.alpha;
                p.image.color = c;

                // 제거
                if (p.posY < bottomY || p.lifetime > p.maxLifetime)
                {
                    Destroy(p.rect.gameObject);
                    activePetals.RemoveAt(i);
                }
            }
        }

        void SpawnPetal()
        {
            Rect area = rectTransform.rect;

            var go = new GameObject($"Petal_{petalIndex++}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();

            img.sprite = petalSprite;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);

            // 크기 — 약간 직사각형으로 꽃잎 느낌
            float size = Random.Range(minSize, maxSize);
            float aspect = Random.Range(0.7f, 1f);
            rt.sizeDelta = new Vector2(size, size * aspect);

            // 스폰 위치
            float spawnX = Random.Range(area.xMin - spawnMarginSide, area.xMax + spawnMarginSide);
            float spawnY = area.yMax + spawnMarginTop;
            rt.anchoredPosition = new Vector2(spawnX, spawnY);

            // 초기 Z 회전
            rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

            // 낙하
            float fallSpeed = Random.Range(minFallSpeed, maxFallSpeed);
            float fallDistance = spawnY - (area.yMin - 80f);
            float maxLife = fallDistance / fallSpeed + 2f;

            var petal = new Petal
            {
                rect = rt,
                image = img,
                fallSpeed = fallSpeed,
                fallAccel = fallSpeed * Random.Range(0.05f, 0.15f), // 미세 속도 변동

                swayAmp1 = Random.Range(minSwayAmp1, maxSwayAmp1),
                swayFreq1 = Random.Range(minSwayFreq1, maxSwayFreq1),
                swayPhase1 = Random.Range(0f, Mathf.PI * 2f),
                swayAmp2 = Random.Range(minSwayAmp2, maxSwayAmp2),
                swayFreq2 = Random.Range(minSwayFreq2, maxSwayFreq2),
                swayPhase2 = Random.Range(0f, Mathf.PI * 2f),

                flipSpeed = Random.Range(minFlipSpeed, maxFlipSpeed),
                flipPhase = Random.Range(0f, Mathf.PI * 2f),
                tiltSpeed = Random.Range(minTiltSpeed, maxTiltSpeed),
                tiltPhase = Random.Range(0f, Mathf.PI * 2f),

                zRotSpeed = Random.Range(minZRotSpeed, maxZRotSpeed) * (Random.value > 0.5f ? 1f : -1f),

                baseX = spawnX,
                posX = spawnX,
                posY = spawnY,
                driftSpeed = Random.Range(-2f, 1.5f),
                windSensitivity = Random.Range(0.3f, 1.2f), // 꽃잎마다 바람 반응 다름

                lifetime = 0f,
                maxLifetime = maxLife,
                alpha = 0f,
                size = size
            };

            activePetals.Add(petal);
        }
    }
}
