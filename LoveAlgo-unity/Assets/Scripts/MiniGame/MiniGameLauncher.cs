using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.MiniGame
{
    /// <summary>
    /// 미니게임 런처 (정적 클래스)
    /// 
    /// CSV Flow 커맨드에서 호출:
    ///   ,Flow,,MiniGame:CherryBlossom:Roa,await
    ///   ,Flow,,MiniGame:Jogging:Yeun,await
    /// 
    /// 형식: MiniGame:{게임이름}:{히로인ID}
    /// 
    /// 스코어 → 포인트 변환:
    ///   - 1차 이벤트(Event1): 스코어 10이상 → +1, 20이상 → +2 (최대 +2)
    ///   - 2차 이벤트(Event2): 스코어 10이상 → +1, 20이상 → +2, 30이상 → +3 (최대 +3)
    /// </summary>
    public static class MiniGameLauncher
    {
        /// <summary>등록된 미니게임 (이름 → 프리팹 경로)</summary>
        static readonly Dictionary<string, string> gameRegistry = new()
        {
            { "CherryBlossom", "MiniGame/CherryBlossomGame" },
            { "Jogging", "MiniGame/JoggingGame" }
        };

        /// <summary>현재 활성 미니게임</summary>
        static MiniGameBase activeGame;

        /// <summary>
        /// 미니게임 시작 (비동기 — 완료까지 대기)
        /// </summary>
        /// <param name="gameName">게임 이름 (CherryBlossom, Jogging 등)</param>
        /// <param name="heroineId">포인트 부여 대상 히로인</param>
        /// <returns>획득한 스코어</returns>
        public static async UniTask<int> LaunchAsync(string gameName, string heroineId)
        {
            Debug.Log($"[MiniGameLauncher] 시작: {gameName} (히로인: {heroineId})");

            // 프리팹 로드
            string prefabPath = gameRegistry.GetValueOrDefault(gameName, $"MiniGame/{gameName}");
            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[MiniGameLauncher] 프리팹 없음: {prefabPath}");
                return 0;
            }

            // 인스턴스 생성
            var go = UnityEngine.Object.Instantiate(prefab);

            // Canvas 하위에 배치 (최상위 UI)
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
                go.transform.SetParent(canvas.transform, false);

            activeGame = go.GetComponent<MiniGameBase>();
            if (activeGame == null)
            {
                Debug.LogError($"[MiniGameLauncher] MiniGameBase 컴포넌트 없음: {gameName}");
                UnityEngine.Object.Destroy(go);
                return 0;
            }

            // 게임 완료 대기
            var tcs = new UniTaskCompletionSource<int>();

            void OnEnd(int score)
            {
                tcs.TrySetResult(score);
            }

            activeGame.OnGameEnd += OnEnd;
            go.SetActive(true);

            int finalScore = await tcs.Task;

            // 정리
            activeGame.OnGameEnd -= OnEnd;
            activeGame = null;

            // 약간의 딜레이 후 파괴 (결과 패널 표시 후)
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
            if (go != null) UnityEngine.Object.Destroy(go);

            // 포인트 변환 및 부여
            int points = ConvertScoreToPoints(finalScore, heroineId);
            if (points > 0)
            {
                HeroinePointTracker.AddPoint(heroineId, PointCategory.MiniGame, points);
                Debug.Log($"[MiniGameLauncher] 포인트 부여: {heroineId} +{points} (스코어: {finalScore})");
            }

            return finalScore;
        }

        /// <summary>
        /// 스코어 → 포인트 변환
        /// 기획서: 1차 최대+2, 2차 최대+3
        /// </summary>
        static int ConvertScoreToPoints(int score, string heroineId)
        {
            // 현재 해당 히로인의 기존 미니게임 포인트 확인
            int existing = HeroinePointTracker.GetPoint(heroineId, PointCategory.MiniGame);
            int maxTotal = 5; // 기획서: 최대 5점
            int remaining = maxTotal - existing;

            if (remaining <= 0) return 0;

            // 스코어 → 포인트 변환 테이블
            int earned;
            if (score >= 30) earned = 3;
            else if (score >= 20) earned = 2;
            else if (score >= 10) earned = 1;
            else earned = 0;

            return Mathf.Min(earned, remaining);
        }

        /// <summary>게임 등록 (런타임 확장용)</summary>
        public static void RegisterGame(string name, string prefabPath)
        {
            gameRegistry[name] = prefabPath;
        }
    }
}
