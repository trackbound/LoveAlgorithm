using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.MiniGame
{
    /// <summary>
    /// 로아 미니게임: 벚꽃잎 잡기
    /// - 떨어지는 벚꽃잎 클릭하면 +1점
    /// - 5% 확률로 벚꽃 한 송이 등장 (+3점, 5배 빠름)
    /// - 30초 플레이, 10초마다 속도 1.5배
    /// </summary>
    public class CherryBlossomGame : MiniGameBase
    {
        [Header("벚꽃 설정")]
        [SerializeField] GameObject petalPrefab;      // 꽃잎 1장
        [SerializeField] GameObject flowerPrefab;    // 꽃 한 송이
        [SerializeField] RectTransform spawnArea;    // 스폰 영역
        [SerializeField] float baseSpawnInterval = 0.5f;
        [SerializeField] float baseFallDuration = 3f;
        [SerializeField] float flowerChance = 0.05f; // 5%

        [Header("점수 UI")]
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text resultScoreText;

        [Header("오디오")]
        [SerializeField] AudioClip catchSound;
        [SerializeField] AudioClip endSound;

        readonly List<GameObject> activePetals = new();
        float spawnTimer;
        float currentSpeedMultiplier = 1f;
        int lastSpeedPhase = 0;

        protected override void Awake()
        {
            base.Awake();
            gameDuration = 30f;
        }

        protected override void OnGameStart()
        {
            // 초기화
            ClearAllPetals();
            currentSpeedMultiplier = 1f;
            lastSpeedPhase = 0;
            spawnTimer = 0f;
            UpdateScoreUI();
        }

        protected override void Update()
        {
            base.Update();
            if (!isPlaying) return;

            // 10초마다 속도 1.5배
            int currentPhase = Mathf.FloorToInt((gameDuration - remainingTime) / 10f);
            if (currentPhase > lastSpeedPhase)
            {
                lastSpeedPhase = currentPhase;
                currentSpeedMultiplier *= 1.5f;
                Debug.Log($"[CherryBlossom] 속도 증가! x{currentSpeedMultiplier:F1}");
            }

            // 스폰
            spawnTimer += Time.deltaTime;
            float adjustedInterval = baseSpawnInterval / currentSpeedMultiplier;
            if (spawnTimer >= adjustedInterval)
            {
                spawnTimer = 0f;
                SpawnPetal();
            }
        }

        void SpawnPetal()
        {
            if (spawnArea == null) return;

            // 5% 확률로 꽃 한 송이
            bool isFlower = Random.value < flowerChance;
            var prefab = isFlower ? flowerPrefab : petalPrefab;
            
            if (prefab == null)
            {
                prefab = petalPrefab; // fallback
                isFlower = false;
            }

            var petal = Instantiate(prefab, spawnArea);
            var rt = petal.GetComponent<RectTransform>();

            // 랜덤 X 위치
            float width = spawnArea.rect.width;
            float randomX = Random.Range(-width / 2f, width / 2f);
            float startY = spawnArea.rect.height / 2f + 50f;
            float endY = -spawnArea.rect.height / 2f - 100f;

            rt.anchoredPosition = new Vector2(randomX, startY);

            // 떨어지는 시간 (꽃은 5배 빠름)
            float fallDuration = baseFallDuration / currentSpeedMultiplier;
            if (isFlower) fallDuration /= 5f;

            // 클릭 이벤트
            var button = petal.GetComponent<Button>();
            if (button == null) button = petal.AddComponent<Button>();
            
            int pointValue = isFlower ? 3 : 1;
            button.onClick.AddListener(() => OnPetalClicked(petal, pointValue));

            // 떨어지는 애니메이션
            activePetals.Add(petal);
            rt.DOAnchorPosY(endY, fallDuration)
                .SetEase(Ease.Linear)
                .OnComplete(() => RemovePetal(petal));
        }

        void OnPetalClicked(GameObject petal, int points)
        {
            if (!isPlaying) return;

            score += points;
            UpdateScoreUI();

            // 효과음
            if (catchSound != null)
                LoveAlgo.Modules.Audio.AudioManager.Instance?.PlaySFXClip(catchSound);

            // 클릭 효과 (스케일 후 제거)
            var rt = petal.GetComponent<RectTransform>();
            rt.DOKill();
            rt.DOScale(1.5f, 0.1f).OnComplete(() => RemovePetal(petal));
        }

        void RemovePetal(GameObject petal)
        {
            if (petal == null) return;
            activePetals.Remove(petal);
            Destroy(petal);
        }

        void ClearAllPetals()
        {
            foreach (var petal in activePetals)
            {
                if (petal != null)
                {
                    petal.GetComponent<RectTransform>()?.DOKill();
                    Destroy(petal);
                }
            }
            activePetals.Clear();
        }

        void UpdateScoreUI()
        {
            if (scoreText != null)
                scoreText.text = score.ToString();
        }

        protected override void EndGame()
        {
            ClearAllPetals();

            // 종료 효과음
            if (endSound != null)
                LoveAlgo.Modules.Audio.AudioManager.Instance?.PlaySFXClip(endSound);

            base.EndGame();
        }

        protected override void ShowResult()
        {
            base.ShowResult();
            
            if (resultScoreText != null)
                resultScoreText.text = score.ToString();
        }
    }
}
