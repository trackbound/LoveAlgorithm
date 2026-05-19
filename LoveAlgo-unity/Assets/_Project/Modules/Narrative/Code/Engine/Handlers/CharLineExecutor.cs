using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Char 라인 실행기 — 캐릭터 제어
    /// </summary>
    public class CharLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.Char;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            // 단축 문법: 첫 토큰이 슬롯(L/C/R/Left/Center/Right)이 아니면 슬롯 C 자동 주입.
            //   "Enter:로아:Default"   → "C:Enter:로아:Default"
            //   "Emote:웃음"           → "C:Emote:웃음"
            //   "Exit"                  → "C:Exit"
            //   "L:Enter:로아:Default" → 그대로 (기존 문법)
            string value = NormalizeCharValue(line.Value);

            var character = ExecutionDependencies.Stage?.Character;
            if (character != null)
            {
                await character.ExecuteAsync(value, ct);
            }
            else
            {
                Debug.Log($"[Char] {value}");
            }
            return true;
        }

        static string NormalizeCharValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            int colon = raw.IndexOf(':');
            string firstToken = colon >= 0 ? raw.Substring(0, colon) : raw;

            // 이미 슬롯이 명시되어 있으면 그대로
            if (CommandAliases.NormalizeSlot(firstToken) != null)
                return raw;

            // 첫 토큰이 Char 액션 키워드면 슬롯 C 주입
            if (CommandAliases.IsCharAction(firstToken))
                return "C:" + raw;

            // 그 외에는 기존 동작 유지 (오타 등은 CharacterLayer가 워닝)
            return raw;
        }
    }
}
