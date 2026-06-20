using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI Canvas 위에서 벚꽃잎이 봄바람에 하늘하늘 흩날리는 파티클 이펙트.
    /// 타이틀 화면의 _UI 캔버스(배경 위, 메뉴 아래 레이어)에 배치해 사용한다.
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
        [SerializeField] int maxPetals = 42;
        [SerializeField] float spawnInterval = 0.55f;

        [Header("크기")]
        [SerializeField] float minSize = 20f;              // 크고 또렷한 꽃잎(옛 버전 느낌)
        [SerializeField] float maxSize = 42f;

        [Header("낙하 (기본 중력)")]
        [SerializeField] float gravity = 8f;               // 기본 중력 가속도 (px/s²)
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

        [Header("3D 텀블 (실제 X/Y 회전)")]
        [Tooltip("Y축 텀블 각속도 범위(도/초) — 모서리·뒷면까지 뒤집히는 주 회전")]
        [SerializeField] float tumbleSpeedYMin = 45f;
        [SerializeField] float tumbleSpeedYMax = 100f;
        [Tooltip("X축 틸트 각속도 범위(도/초) — 위아래로 기우뚱")]
        [SerializeField] float tumbleSpeedXMin = 20f;
        [SerializeField] float tumbleSpeedXMax = 55f;

        [Header("Z 회전 (평면 스핀)")]
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

        [Header("웜업 (타이틀 진입 시 이미 흩날리는 상태)")]
        [Tooltip("OnEnable 시 화면 전체에 미리 배치할 꽃잎 수 (maxPetals의 비율 0~1)")]
        [SerializeField, Range(0f, 1f)] float warmUpFill = 0.7f;  // 70% 채우고 시작

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
            public float noiseSeedZRot;

            // 3D 텀블 (실제 오일러 각 누적 — euler 읽기 역계산이 불안정해 직접 보관)
            public float rotX, rotY, rotZ;           // 현재 누적 X/Y/Z 각(도)
            public float tumbleSpeedX, tumbleSpeedY; // X/Y 텀블 각속도(부호=회전 방향)

            // 개별 속성
            public float windSensitivity;     // 바람 감도
            public float mass;                // 질량 (크기에 비례 → 큰 꽃잎은 더 느리게 반응)
            public float zRotSpeed;            // Z(평면) 회전 드리프트 속도
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
            WarmUp();
        }

        /// <summary>
        /// 화면 전체에 꽃잎을 미리 배치 — 타이틀 등장 시 이미 흩날리는 상태.
        /// 각 꽃잎은 화면 내 랜덤 위치에 생성되고, 수명도 중간부터 시작.
        /// </summary>
        void WarmUp()
        {
            if (rectTransform == null) return;
            Rect area = rectTransform.rect;

            int count = Mathf.RoundToInt(maxPetals * warmUpFill);
            for (int i = 0; i < count; i++)
            {
                // 화면 내 랜덤 Y (위~아래 전체 분포)
                float randomY = Random.Range(area.yMin + 40f, area.yMax - 20f);
                // 수명 진행도: 위쪽이면 초반, 아래쪽이면 후반 (자연스러운 분포)
                float yRatio = (randomY - area.yMin) / Mathf.Max(1f, area.yMax - area.yMin);
                // 수명 진행도를 랜덤하게 — fadeIn은 넘긴 상태로
                float lifeFraction = Mathf.Lerp(0.15f, 0.7f, 1f - yRatio) + Random.Range(-0.1f, 0.1f);
                lifeFraction = Mathf.Clamp01(lifeFraction);

                SpawnPetal(randomY, lifeFraction);
            }
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

                // ── 3D 텀블 (실제 X/Y 오일러 회전 — 모서리·뒷면까지 뒤집힘) ──
                // Overlay(직교) 캔버스라 원근은 없지만, 실제 회전은 edge-on을 지나
                // 뒷면(UI 셰이더 Cull Off)까지 드러나 진짜 입체 텀블로 보인다.
                p.rotY += p.tumbleSpeedY * dt;
                p.rotX += p.tumbleSpeedX * dt;

                // ── Z(평면) 회전: Perlin 변조 — 방향이 서서히 바뀜 ──
                float zRotNoise = Mathf.PerlinNoise(t * zRotNoiseSpeed + p.noiseSeedZRot, p.noiseSeedZRot + 40f);
                float zRotDir = (zRotNoise - 0.5f) * 2f;  // -1 ~ +1
                p.rotZ += p.zRotSpeed * zRotDir * dt;

                p.rect.localEulerAngles = new Vector3(p.rotX, p.rotY, p.rotZ);

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

        /// <summary>
        /// 꽃잎 생성. overrideY/lifeFraction은 WarmUp에서 화면 내 배치 시 사용.
        /// </summary>
        void SpawnPetal(float overrideY = float.NaN, float lifeFraction = 0f)
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

            // 크기 — 균등 분포(옛 버전: 크고 또렷한 꽃잎)
            float size = Random.Range(minSize, maxSize);
            float aspect = Random.Range(0.65f, 1f);
            rt.sizeDelta = new Vector2(size, size * aspect);

            // 스폰 위치
            float spawnX = Random.Range(area.xMin - spawnMarginSide, area.xMax + spawnMarginSide);
            float spawnY = float.IsNaN(overrideY) ? area.yMax + spawnMarginTop : overrideY;
            rt.anchoredPosition = new Vector2(spawnX, spawnY);

            // 초기 텀블 각(랜덤 위상) + 방향(좌/우 절반씩)
            float initRotX = Random.Range(0f, 360f);
            float initRotY = Random.Range(0f, 360f);
            float initRotZ = Random.Range(0f, 360f);
            float dirX = Random.value < 0.5f ? -1f : 1f;
            float dirY = Random.value < 0.5f ? -1f : 1f;
            rt.localEulerAngles = new Vector3(initRotX, initRotY, initRotZ);

            // 질량: 크기에 비례 — 큰 꽃잎은 관성이 커서 느리게 반응
            float normalizedSize = (size - minSize) / Mathf.Max(1f, maxSize - minSize);
            float mass = Mathf.Lerp(0.6f, 1.4f, normalizedSize);

            // 낙하 거리 기준 수명 계산
            float topY = area.yMax + spawnMarginTop;
            float fallDistance = topY - (area.yMin - 80f);
            float avgFall = gravity / (airDrag * 0.5f + 0.1f);
            float maxLife = fallDistance / Mathf.Max(avgFall, 1f) + 5f;

            // WarmUp: 수명을 중간부터 시작 (이미 떨어지고 있던 것처럼)
            float startLife = maxLife * lifeFraction;

            // Perlin noise 시드
            float seed = Random.Range(0f, 1000f);

            // WarmUp 꽃잎은 이미 알파가 올라온 상태
            float startAlphaVal = lifeFraction > fadeInRatio ? peakAlpha : 0f;

            var petal = new Petal
            {
                rect = rt,
                image = img,

                velX = Random.Range(-1.5f, 1.5f),
                velY = Random.Range(0.5f, 2f),

                posX = spawnX,
                posY = spawnY,

                noiseSeedX = seed,
                noiseSeedY = seed + 100f,
                noiseSeedSway = seed + 200f,
                noiseSeedHover = seed + 300f,
                noiseSeedZRot = seed + 600f,

                rotX = initRotX,
                rotY = initRotY,
                rotZ = initRotZ,
                tumbleSpeedX = dirX * Random.Range(tumbleSpeedXMin, tumbleSpeedXMax),
                tumbleSpeedY = dirY * Random.Range(tumbleSpeedYMin, tumbleSpeedYMax),

                windSensitivity = Random.Range(0.4f, 1.3f),
                mass = mass,
                zRotSpeed = Random.Range(minZRotSpeed, maxZRotSpeed),
                size = size,

                lifetime = startLife,
                maxLifetime = maxLife,
                alpha = startAlphaVal
            };

            // WarmUp 꽃잎은 바로 보이게
            if (startAlphaVal > 0f)
            {
                var c = img.color;
                c.a = startAlphaVal;
                img.color = c;
            }

            activePetals.Add(petal);
        }
    }
}
