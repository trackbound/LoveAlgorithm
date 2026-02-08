using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.MiniGame
{
    /// <summary>
    /// 미니게임 공통 베이스 클래스
    /// </summary>
    public abstract class MiniGameBase : MonoBehaviour
    {
        [Header("공통 UI")]
        [SerializeField] protected GameObject gamePanel;
        [SerializeField] protected GameObject introPanel;
        [SerializeField] protected GameObject resultPanel;
        [SerializeField] protected Button startButton;
        [SerializeField] protected Button backButton;
        [SerializeField] protected Button resultConfirmButton;
        [SerializeField] protected TMP_Text timerText;

        [Header("설정")]
        [SerializeField] protected float gameDuration = 30f;

        protected float remainingTime;
        protected bool isPlaying;
        protected int score;

        // 게임 결과 콜백
        public event Action<int> OnGameEnd;

        protected virtual void Awake()
        {
            startButton?.onClick.AddListener(StartGame);
            backButton?.onClick.AddListener(GoBack);
            resultConfirmButton?.onClick.AddListener(CloseResult);
        }

        protected virtual void OnEnable()
        {
            ShowIntro();
        }

        protected virtual void Update()
        {
            if (!isPlaying) return;

            remainingTime -= Time.deltaTime;
            UpdateTimerUI();

            if (remainingTime <= 0)
            {
                EndGame();
            }
        }

        #region 게임 흐름

        public void ShowIntro()
        {
            introPanel?.SetActive(true);
            gamePanel?.SetActive(false);
            resultPanel?.SetActive(false);
        }

        public virtual void StartGame()
        {
            introPanel?.SetActive(false);
            gamePanel?.SetActive(true);
            resultPanel?.SetActive(false);

            score = 0;
            remainingTime = gameDuration;
            isPlaying = true;

            OnGameStart();
        }

        protected virtual void EndGame()
        {
            isPlaying = false;
            OnGameEnd?.Invoke(score);
            ShowResult();
        }

        protected virtual void ShowResult()
        {
            gamePanel?.SetActive(false);
            resultPanel?.SetActive(true);
        }

        protected virtual void CloseResult()
        {
            resultPanel?.SetActive(false);
            gameObject.SetActive(false);
        }

        protected virtual void GoBack()
        {
            gameObject.SetActive(false);
        }

        #endregion

        #region 추상/가상 메서드

        protected abstract void OnGameStart();

        protected virtual void UpdateTimerUI()
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(remainingTime);
                timerText.text = seconds.ToString();
            }
        }

        #endregion
    }
}
