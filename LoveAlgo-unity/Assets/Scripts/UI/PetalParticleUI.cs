using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI Canvas 위에서 벚꽃잎이 봄바람에 하늘하늘 흩날리는 파티클 이펙트.
    /// 
    /// 핵심 알고리즘:
    /// - Perlin noise 기반 바람: 반복 없는 유기적 흐름
    /// - 속도 누적 물리: 바람이 위치가 아닌 속도에 작용 → 관성·감속 자연스러움
    /// - X/Y 커플링: 느리게 떨어질수록 더 많이 떠다님
    /// - Hover 모멘트: Perlin 기반 부력으로 가끔 거의 멈추고 부유
    /// - 3D 뒤집힘: Perlin 변조 스케일 오실레이션
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

        [Header("낙하 (기본 중력)")]
        [SerializeField] float gravity = 8f;              // 기본 중력 가속도 (px/s²)
        [SerializeField] float terminalSpeed = 16f;        // 최대 낙하 속도
        [SerializeField] float airDrag = 1.8f;             // 공기 저항 — 속도에 비례하는 감속

        [Header("바람 (Perlin noise 기반)")]
        [SerializeField] float windDriftX = 3f;            // 평균 횡방향 바람
        [SerializeField] float windNoiseStrength = 6f;     // Perlin 바람 강도 (X)
        [SerializeField] float windNoiseSpeed = 0.15f;     // Perlin 변화 속도
        [SerializeField] float windVertStrength = 5f;      // 수직 바람 (부력)
        [SerializeField] float windVertNoiseSpeed = 0.1f;  // 수직 Perlin 속도

        [Header("개별 흔들림 (Perlin noise)")]
        [SerializeField] float swayStrength = 30f;         // 흔들림 강도 (px)
        [SerializeField] float swaySpeed = 0.3f;           // Perlin 스크롤 속도

        [Header("부유 (Hover)")]
        [Tooltip("Perlin 기반 부력 — 가끔 낙하를 거의 멈추고 떠다님")]
        [SerializeField] float hoverStrength = 12f;        // 부력 최대 강도
        [SerializeField] float hoverNoiseSpeed = 0.08f;    // 부력 변화 주기

        [Header("3D 회전 시뮬레이션")]
        [SerializeField] float flipMinScale = 0.4f;
        [SerializeField] float flipNoiseSpeed = 0.25f;     // Perlin 기반 뒤집힘 속도
        [SerializeField] float tiltNoiseSpeed = 0.2f;

        [Header("Z 회전")]
        [SerializeField] float minZRotSpeed = 2f;
        [SerializeField] float maxZRotSpeed = 8f;
        [SerializeField] float zRotNoiseSpeed = 0.15f;     // Perlin 변조 — 회전 방향이 서서히 바뀜

        [Header("투명도")]
        [SerializeField] float startAlpha = 0f;
        [SerializeField] float peakAlpha = 0.7f;
        [SerializeField] float fadeInRatio = 0.2f;
        [SerializeField] float fadeOutRatio = 0.3f;

        [Header("스폰 영역")]
        [SerializeField] float spawnMarginTop = 60f;
        [SerializeField] float spawnMarginSide = 120f;

        RectTransform rectTransform;
        readonly List<Petal> activePetals = new();
        float spawnTimer;
        int petalIndex;
        float globalTime;

        class Petal
        {
            public RectTransform rect;
            public Image image;

            // 물리 상태 (속도 기반)
            public float velX, velY;          // 현재 속도
            public float posX, posY;          // 현재 위치

            // Perlin noise 시드 (꽃잎마다 고유)
            public float noiseSeedX;
            public float noiseSeedY;
            public float noiseSeedSway;
            public float noiseSeedHover;
            public float noiseSeedFlip;
            public float noiseSeedTilt;
            public float noiseSeedZRot;

            // 개별 속성
            public float windSensitivity;     // 바람 감도
            public float mass;                // 질량 (크기에 비례 → 큰 꽃잎은 더 느리게 반응)
            public float zRotSpeed;
            public float size;

            // 수명
            public float lifetime, maxLifetime;
            public float alpha;
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
            if (dt <= 0f) return;
            globalTime += dt;

            // ── 스폰 ──
            spawnTimer += dt;
            if (spawnTimer >= spawnInterval && activePetals.Count < maxPetals)
            {
                spawnTimer = 0f;
                SpawnPetal();
            }

            // ── 글로벌 바람 (Perlin noise) ──
            // 글로벌 시간축 Perlin → 전체적으로 일관된 바람의 흐름 + 비반복
            float gWindX = windDriftX
                + windNoiseStrength * (Mathf.PerlinNoise(globalTime * windNoiseSpeed, 0f) - 0.5f) * 2f
                + windNoiseStrength * 0.4f * (Mathf.PerlinNoise(globalTime * windNoiseSpeed * 1.7f, 5f) - 0.5f) * 2f;

            float gWindY = windVertStrength * (Mathf.PerlinNoise(globalTime * windVertNoiseSpeed, 10f) - 0.5f) * 2f;

            // ── 꽃잎 업데이트 ──
            Rect area = rectTransform.rect;
            float bottomY = area.yMin - 80f;

            for (int i = activePetals.Count - 1; i >= 0; i--)
            {
                var p = activePetals[i];
                p.lifetime += dt;
                float t = p.lifetime;

                // ── 개별 Perlin 힘 ──
                // 흔들림: 꽃잎 고유 Perlin → 비주기적 좌우 움직임
                float swayForce = swayStrength
                    * (Mathf.PerlinNoise(t * swaySpeed + p.noiseSeedSway, p.noiseSeedSway) - 0.5f) * 2f;

                // 부력 (hover): 가끔 낙하를 거의 멈추는 순간
                float hoverNoise = Mathf.PerlinNoise(t * hoverNoiseSpeed + p.noiseSeedHover, p.noiseSeedHover + 20f);
                // 0.55 이상일 때만 부력 발생 → 간헐적 hover
                float hoverForce = hoverNoise > 0.55f
                    ? hoverStrength * Mathf.Pow((hoverNoise - 0.55f) / 0.45f, 2f)
                    : 0f;

                // ── 힘 → 가속도 (질량 반영) ──
                float invMass = 1f / p.mass;

                // X 가속: 글로벌 바람 + 개별 흔들림 + 개별 바람 Perlin
                float windForceX = gWindX * p.windSensitivity
                    + windNoiseStrength * 0.5f * (Mathf.PerlinNoise(t * windNoiseSpeed * 0.7f + p.noiseSeedX, p.noiseSeedX) - 0.5f) * 2f;
                float accelX = (windForceX + swayForce) * invMass;

                // Y 가속: 중력 - 부력 - 수직 바람 - 공기 저항
                float gravForce = gravity;
                float vertWind = gWindY * p.windSensitivity;
                float accelY = (gravForce - hoverForce - Mathf.Max(0f, vertWind)) * invMass;

                // ── X/Y 커플링: 느리게 떨어질수록 더 많이 흔들림 ──
                float fallRatio = Mathf.Clamp01(Mathf.Abs(p.velY) / terminalSpeed);
                float couplingBoost = Mathf.Lerp(1.8f, 0.8f, fallRatio);  // 느릴 때 1.8x, 빠를 때 0.8x
                accelX *= couplingBoost;

                // ── 속도 업데이트 (공기 저항 적용) ──
                p.velX += accelX * dt;
                p.velY += accelY * dt;

                // 공기 저항: 속도에 비례하는 감속
                p.velX *= 1f / (1f + airDrag * dt);
                p.velY *= 1f / (1f + airDrag * 0.5f * dt);  // Y는 저항 약하게 (아래로는 잘 떨어지도록)

                // 최대 속도 제한
                p.velY = Mathf.Min(p.velY, terminalSpeed);
                p.velY = Mathf.Max(p.velY, -terminalSpeed * 0.3f);  // 위로 너무 날아가지 않게

                // ── 위치 갱신 ──
                p.posX += p.velX * dt;
                p.posY -= p.velY * dt;  // velY 양수 = 아래로

                p.rect.anchoredPosition = new Vector2(p.posX, p.posY);

                // ── 3D 회전 시뮬레이션 (Perlin 기반) ──
                float flipNoise = Mathf.PerlinNoise(t * flipNoiseSpeed + p.noiseSeedFlip, p.noiseSeedFlip);
                float scaleX = Mathf.Lerp(flipMinScale, 1f, flipNoise);

                float tiltNoise = Mathf.PerlinNoise(t * tiltNoiseSpeed + p.noiseSeedTilt, p.noiseSeedTilt + 30f);
                float scaleY = Mathf.Lerp(flipMinScale + 0.15f, 1f, tiltNoise);

                p.rect.localScale = new Vector3(scaleX, scaleY, 1f);

                // ── Z 회전 (Perlin 변조 — 방향이 서서히 바뀜) ──
                float zRotNoise = Mathf.PerlinNoise(t * zRotNoiseSpeed + p.noiseSeedZRot, p.noiseSeedZRot + 40f);
                float zRotDir = (zRotNoise - 0.5f) * 2f;  // -1 ~ +1
                float rot = p.rect.localEulerAngles.z + p.zRotSpeed * zRotDir * dt;
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

            // 질량: 크기에 비례 — 큰 꽃잎은 관성이 커서 느리게 반응
            float normalizedSize = (size - minSize) / Mathf.Max(1f, maxSize - minSize);
            float mass = Mathf.Lerp(0.6f, 1.4f, normalizedSize);

            // 낙하 거리 기준 수명 계산 (gravity 기반 추정)
            float fallDistance = spawnY - (area.yMin - 80f);
            float avgFall = gravity / (airDrag * 0.5f + 0.1f);
            float maxLife = fallDistance / Mathf.Max(avgFall, 1f) + 4f;

            // Perlin noise 시드 — 꽃잎마다 완전히 다른 노이즈 경로
            float seed = Random.Range(0f, 1000f);

            var petal = new Petal
            {
                rect = rt,
                image = img,

                velX = Random.Range(-1f, 1f),   // 약간의 초기 속도
                velY = Random.Range(0.5f, 2f),  // 천천히 낙하 시작

                posX = spawnX,
                posY = spawnY,

                noiseSeedX = seed,
                noiseSeedY = seed + 100f,
                noiseSeedSway = seed + 200f,
                noiseSeedHover = seed + 300f,
                noiseSeedFlip = seed + 400f,
                noiseSeedTilt = seed + 500f,
                noiseSeedZRot = seed + 600f,

                windSensitivity = Random.Range(0.4f, 1.3f),
                mass = mass,
                zRotSpeed = Random.Range(minZRotSpeed, maxZRotSpeed),
                size = size,

                lifetime = 0f,
                maxLifetime = maxLife,
                alpha = 0f
            };

            activePetals.Add(petal);
        }
    }
}
