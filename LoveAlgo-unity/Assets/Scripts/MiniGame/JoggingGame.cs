using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.MiniGame
{
    /// <summary>
    /// 예은 미니게임: 조깅
    /// - 스페이스바 연타로 예은이와 속도 맞추기
    /// - 너무 빠르거나 느리면 실패
    /// - 60초 플레이
    /// </summary>
    public class JoggingGame : MiniGameBase
    {
        [Header("캐릭터")]
        [SerializeField] RectTransform playerTransform;
        [SerializeField] RectTransform yeunTransform;
        [SerializeField] Image yeunFireEffect; // 최대 속도 시 불 효과

        [Header("게이지")]
        [SerializeField] Slider speedGauge;
        [SerializeField] Image slowZone;
        [SerializeField] Image normalZone;
        [SerializeField] Image rushZone;

        [Header("말풍선")]
        [SerializeField] GameObject speechBubble;
        [SerializeField] TMP_Text speechText;
        [SerializeField] float speechDuration = 1.7f;

        [Header("결과")]
        [SerializeField] TMP_Text resultTimeText;
        [SerializeField] GameObject failPanel;
        [SerializeField] TMP_Text failText;

        [Header("설정")]
        [SerializeField] float startX = -400f;
        [SerializeField] float finishX = 400f;
        [SerializeField] float gaugeDecayRate = 0.3f;  // 게이지 감소 속도
        [SerializeField] float gaugeIncrement = 0.08f; // 스페이스바 당 증가량

        // 예은 속도 타임라인 (초, 속도)
        readonly (int time, int speed)[] speedTimeline = {
            (0, 0), (1, 1), (4, 2), (9, 3), (14, 4), (21, 5),
            (26, 2), (30, 4), (33, 3), (37, 5), (44, 2), (49, 4),
            (54, 1), (57, 4)
        };

        // 구간별 대사
        readonly Dictionary<int, string> phaseSpeech = new()
        {
            { 1, "준비됐어?" },
            { 4, "슬슬 몸 풀자!" },
            { 9, "러닝은 개운한 거야!" },
            { 14, "설마 힘든 건 아니겠지?" },
            { 21, "따라와 봐!" },
            { 26, "재밌지?" },
            { 33, "생각보다 잘하는데!" },
            { 37, "달려보자고!" },
            { 54, "괜찮아, 바보야?" },
            { 57, "마지막 스피드!" }
        };

        // 예은이 더 빠를 때 대사
        readonly string[] yeunFasterSpeech = {
            "따라와 봐!",
            "이래서 같이 뛸 수 있겠어?",
            "여전히 느리다니까!"
        };

        // 플레이어가 더 빠를 때 대사
        readonly string[] playerFasterSpeech = {
            "잠깐만, 이 바보!",
            "왜 이렇게 빠른거야?!",
            "두고 가면 어떡해?!"
        };

        float playerGauge; // 0~1
        float playerPosition;
        float yeunPosition;
        float playerFinishTime;
        float yeunFinishTime;
        bool playerFinished;
        bool yeunFinished;
        int currentYeunSpeed;
        int lastShownPhase = -1;

        protected override void Awake()
        {
            base.Awake();
            gameDuration = 60f;
        }

        protected override void OnGameStart()
        {
            playerGauge = 0f;
            playerPosition = startX;
            yeunPosition = startX;
            playerFinished = false;
            yeunFinished = false;
            playerFinishTime = 0f;
            yeunFinishTime = 0f;
            currentYeunSpeed = 0;
            lastShownPhase = -1;

            UpdatePositions();
            UpdateGaugeUI();
            HideSpeechBubble();
            failPanel?.SetActive(false);
            yeunFireEffect?.gameObject.SetActive(false);
        }

        protected override void Update()
        {
            if (!isPlaying) return;

            float elapsed = gameDuration - remainingTime;

            // 스페이스바 입력
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                playerGauge = Mathf.Clamp01(playerGauge + gaugeIncrement);
            }

            // 게이지 자연 감소
            playerGauge = Mathf.Clamp01(playerGauge - gaugeDecayRate * Time.deltaTime);
            UpdateGaugeUI();

            // 예은 속도 업데이트
            UpdateYeunSpeed(elapsed);

            // 이동
            if (!playerFinished)
            {
                float playerSpeed = GetPlayerSpeedFromGauge();
                playerPosition += playerSpeed * Time.deltaTime;
                
                if (playerPosition >= finishX)
                {
                    playerPosition = finishX;
                    playerFinished = true;
                    playerFinishTime = elapsed;
                }
            }

            if (!yeunFinished)
            {
                float yeunSpeed = GetYeunSpeedValue();
                yeunPosition += yeunSpeed * Time.deltaTime;
                
                if (yeunPosition >= finishX)
                {
                    yeunPosition = finishX;
                    yeunFinished = true;
                    yeunFinishTime = elapsed;
                }
            }

            UpdatePositions();

            // 불 효과
            if (yeunFireEffect != null)
                yeunFireEffect.gameObject.SetActive(currentYeunSpeed >= 5);

            // 속도 차이 체크 및 대사
            CheckSpeedDifference(elapsed);

            // 타이머 업데이트
            remainingTime -= Time.deltaTime;
            UpdateTimerUI();

            // 둘 다 도착하면 종료
            if (playerFinished && yeunFinished)
            {
                EndGame();
            }
            else if (remainingTime <= 0)
            {
                EndGame();
            }
        }

        void UpdateYeunSpeed(float elapsed)
        {
            int elapsedInt = Mathf.FloorToInt(elapsed);
            
            // 현재 구간의 속도 찾기
            int newSpeed = 0;
            int phaseTime = 0;
            for (int i = speedTimeline.Length - 1; i >= 0; i--)
            {
                if (elapsedInt >= speedTimeline[i].time)
                {
                    newSpeed = speedTimeline[i].speed;
                    phaseTime = speedTimeline[i].time;
                    break;
                }
            }

            currentYeunSpeed = newSpeed;

            // 구간 시작 대사
            if (phaseTime != lastShownPhase && phaseSpeech.TryGetValue(phaseTime, out string speech))
            {
                lastShownPhase = phaseTime;
                ShowSpeech(speech);
            }
        }

        void CheckSpeedDifference(float elapsed)
        {
            float positionDiff = playerPosition - yeunPosition;
            float timeDiff = Mathf.Abs(positionDiff) / 50f; // 대략적인 시간 차이 계산

            if (timeDiff >= 3f)
            {
                if (positionDiff > 0)
                {
                    // 플레이어가 더 빠름
                    ShowSpeech(playerFasterSpeech[Random.Range(0, playerFasterSpeech.Length)]);
                }
                else
                {
                    // 예은이 더 빠름
                    ShowSpeech(yeunFasterSpeech[Random.Range(0, yeunFasterSpeech.Length)]);
                }
            }
        }

        float GetPlayerSpeedFromGauge()
        {
            // 게이지에 따른 속도 (0~1 → 0~5 스케일)
            int speedLevel = Mathf.FloorToInt(playerGauge * 5f);
            return GetSpeedValue(speedLevel);
        }

        float GetYeunSpeedValue()
        {
            return GetSpeedValue(currentYeunSpeed);
        }

        float GetSpeedValue(int level)
        {
            // 속도 레벨에 따른 실제 이동 속도
            return level switch
            {
                0 => 0f,
                1 => 30f,
                2 => 50f,
                3 => 70f,
                4 => 90f,
                5 => 120f,
                _ => 0f
            };
        }

        void UpdatePositions()
        {
            if (playerTransform != null)
                playerTransform.anchoredPosition = new Vector2(playerPosition, playerTransform.anchoredPosition.y);
            
            if (yeunTransform != null)
                yeunTransform.anchoredPosition = new Vector2(yeunPosition, yeunTransform.anchoredPosition.y);
        }

        void UpdateGaugeUI()
        {
            if (speedGauge != null)
                speedGauge.value = playerGauge;
        }

        void ShowSpeech(string text)
        {
            if (speechBubble == null || speechText == null) return;

            speechText.text = text;
            speechBubble.SetActive(true);

            // 자동 숨김
            CancelInvoke(nameof(HideSpeechBubble));
            Invoke(nameof(HideSpeechBubble), speechDuration);
        }

        void HideSpeechBubble()
        {
            speechBubble?.SetActive(false);
        }

        protected override void EndGame()
        {
            isPlaying = false;

            // 결과 판정
            float timeDiff = playerFinishTime - yeunFinishTime;
            
            bool isFail = false;
            string failMessage = "";

            if (timeDiff < -3f) // 플레이어가 3초 이상 빠름
            {
                isFail = true;
                failMessage = $"씨이... 왜 나보다 {Mathf.Abs(timeDiff):F1}초나 빠른거야?\n컴퓨터나 하는 약골인 줄 알았더니! 진짜 짜증나!";
            }
            else if (timeDiff > 4f) // 플레이어가 4초 이상 느림
            {
                isFail = true;
                failMessage = $"나보다 {timeDiff:F1}초나 느리네?\n넌 나를 이기려면 한참 멀었다, 바보야!";
            }

            if (isFail && failPanel != null)
            {
                failPanel.SetActive(true);
                if (failText != null) failText.text = failMessage;
            }

            // 스코어 계산: 성공 시 시간 차이 기반 (작을수록 높은 점수)
            if (!isFail)
            {
                float absDiff = Mathf.Abs(timeDiff);
                if (absDiff <= 0.5f) score = 30;       // 거의 동시 → 최고점
                else if (absDiff <= 1.5f) score = 20;   // 근접
                else score = 10;                         // 통과
            }
            else
            {
                score = 0; // 실패
            }

            // base에서 OnGameEnd?.Invoke(score) 호출
            InvokeGameEnd(score);

            ShowResult();
        }

        protected override void ShowResult()
        {
            base.ShowResult();

            if (resultTimeText != null)
                resultTimeText.text = $"{playerFinishTime:F1}초";
        }
    }
}
