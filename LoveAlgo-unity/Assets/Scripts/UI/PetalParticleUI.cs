using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI Canvas 위에서 벚꽃잎이 회전하며 흩날리는 파티클 이펙트
    /// RectTransform 기반으로 동작하여 UI 레이어 순서 제어 가능
    /// </summary>
    public class PetalParticleUI : MonoBehaviour
    {
        [Header("꽃잎 설정")]
        [SerializeField] Sprite petalSprite;
        [SerializeField] int maxPetals = 25;
        [SerializeField] float spawnInterval = 0.4f;

        [Header("크기")]
        [SerializeField] float minSize = 20f;
        [SerializeField] float maxSize = 45f;

        [Header("속도")]
        [SerializeField] float minFallSpeed = 30f;
        [SerializeField] float maxFallSpeed = 80f;
        [SerializeField] float minHorizontalSpeed = -25f;
        [SerializeField] float maxHorizontalSpeed = 15f;

        [Header("흔들림 (사인파)")]
        [SerializeField] float minSwayAmplitude = 15f;
        [SerializeField] float maxSwayAmplitude = 40f;
        [SerializeField] float minSwayFrequency = 0.5f;
        [SerializeField] float maxSwayFrequency = 1.5f;

        [Header("회전")]
        [SerializeField] float minRotateSpeed = 30f;
        [SerializeField] float maxRotateSpeed = 120f;

        [Header("투명도")]
        [SerializeField] float startAlpha = 0f;
        [SerializeField] float peakAlpha = 0.7f;
        [SerializeField] float fadeInRatio = 0.1f;   // 전체 수명 중 fade-in 비율
        [SerializeField] float fadeOutRatio = 0.2f;  // 전체 수명 중 fade-out 비율

        [Header("스폰 영역 (RectTransform 기준)")]
        [SerializeField] float spawnMarginTop = 50f;     // 상단 위로 얼마나 더 올릴지
        [SerializeField] float spawnMarginSide = 100f;   // 좌우로 얼마나 넓힐지

        RectTransform rectTransform;
        List<Petal> activePetals = new List<Petal>();
        float spawnTimer;
        int petalIndex;

        class Petal
        {
            public RectTransform rect;
            public Image image;
            public float fallSpeed;
            public float horizontalSpeed;
            public float swayAmplitude;
            public float swayFrequency;
            public float swayPhase;
            public float rotateSpeed;
            public float baseX;
            public float lifetime;
            public float maxLifetime;
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
            // 비활성화 시 모든 꽃잎 정리
            foreach (var petal in activePetals)
            {
                if (petal.rect != null)
                    Destroy(petal.rect.gameObject);
            }
            activePetals.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // 스폰
            spawnTimer += dt;
            if (spawnTimer >= spawnInterval && activePetals.Count < maxPetals)
            {
                spawnTimer = 0f;
                SpawnPetal();
            }

            // 업데이트
            Rect area = rectTransform.rect;
            float bottomY = area.yMin - 50f;

            for (int i = activePetals.Count - 1; i >= 0; i--)
            {
                var p = activePetals[i];
                p.lifetime += dt;

                // 이동
                float sway = p.swayAmplitude * Mathf.Sin(p.swayFrequency * p.lifetime * Mathf.PI * 2f + p.swayPhase);
                float x = p.baseX + sway + p.horizontalSpeed * p.lifetime;
                float y = p.rect.anchoredPosition.y - p.fallSpeed * dt;
                p.rect.anchoredPosition = new Vector2(x, y);

                // 회전
                float rot = p.rect.localEulerAngles.z + p.rotateSpeed * dt;
                p.rect.localEulerAngles = new Vector3(0, 0, rot);

                // 알파 (fade-in → 유지 → fade-out)
                float lifeRatio = p.lifetime / p.maxLifetime;
                if (lifeRatio < fadeInRatio)
                {
                    p.alpha = Mathf.Lerp(startAlpha, peakAlpha, lifeRatio / fadeInRatio);
                }
                else if (lifeRatio > (1f - fadeOutRatio))
                {
                    p.alpha = Mathf.Lerp(peakAlpha, 0f, (lifeRatio - (1f - fadeOutRatio)) / fadeOutRatio);
                }
                else
                {
                    p.alpha = peakAlpha;
                }

                var c = p.image.color;
                c.a = p.alpha;
                p.image.color = c;

                // 화면 밖이거나 수명 초과 시 제거
                if (y < bottomY || p.lifetime > p.maxLifetime)
                {
                    Destroy(p.rect.gameObject);
                    activePetals.RemoveAt(i);
                }
            }
        }

        void SpawnPetal()
        {
            Rect area = rectTransform.rect;

            // 꽃잎 GameObject 생성
            var go = new GameObject($"Petal_{petalIndex++}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();

            // 스프라이트 설정
            img.sprite = petalSprite;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f); // 시작 시 투명

            // 크기
            float size = Random.Range(minSize, maxSize);
            rt.sizeDelta = new Vector2(size, size);

            // 시작 위치 — 상단 + 약간의 마진에서 랜덤
            float spawnX = Random.Range(area.xMin - spawnMarginSide, area.xMax + spawnMarginSide);
            float spawnY = area.yMax + spawnMarginTop;
            rt.anchoredPosition = new Vector2(spawnX, spawnY);

            // 초기 회전
            rt.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 360f));

            // 낙하 거리로 최대 수명 계산
            float fallDistance = spawnY - (area.yMin - 50f);
            float fallSpeed = Random.Range(minFallSpeed, maxFallSpeed);
            float maxLife = fallDistance / fallSpeed + 1f; // 여유 마진

            var petal = new Petal
            {
                rect = rt,
                image = img,
                fallSpeed = fallSpeed,
                horizontalSpeed = Random.Range(minHorizontalSpeed, maxHorizontalSpeed),
                swayAmplitude = Random.Range(minSwayAmplitude, maxSwayAmplitude),
                swayFrequency = Random.Range(minSwayFrequency, maxSwayFrequency),
                swayPhase = Random.Range(0f, Mathf.PI * 2f),
                rotateSpeed = Random.Range(minRotateSpeed, maxRotateSpeed) * (Random.value > 0.5f ? 1f : -1f),
                baseX = spawnX,
                lifetime = 0f,
                maxLifetime = maxLife,
                alpha = 0f,
                size = size
            };

            activePetals.Add(petal);
        }
    }
}
