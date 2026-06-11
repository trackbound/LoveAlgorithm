using System;

namespace LoveAlgo.Story
{
    /// <summary>Flow 메신저 명령 파싱 결과 — 시퀀스 id + 읽힘 대기 여부.</summary>
    public readonly struct MessengerCommandIntent
    {
        public readonly bool IsValid;
        public readonly string SequenceId;
        /// <summary>true면 유저가 시퀀스를 끝까지 읽을 때까지 스크립트 진행을 멈춘다("확인 필수 메시지").</summary>
        public readonly bool Wait;

        public MessengerCommandIntent(string sequenceId, bool wait)
        {
            IsValid = !string.IsNullOrEmpty(sequenceId);
            SequenceId = sequenceId;
            Wait = wait;
        }
    }

    /// <summary>
    /// Flow <c>Messenger:{시퀀스id}[:Wait]</c> 순수 파서(EventBus·UnityEngine 비의존, EditMode 테스트).
    /// 구 엔진 어휘 <c>Message</c>를 별칭으로 수용(작가 문법 호환, ADR-009). Wait 외 토큰은 무시(관용) —
    /// 대기 오타는 "안 기다림"으로 강등되어 hang보다 안전한 쪽으로 실패한다.
    /// </summary>
    public static class MessengerCommandParser
    {
        /// <summary>이 Flow 값이 메신저 가족(Messenger/Message)인가 — Run 루프의 대기형 분기 판별.</summary>
        public static bool IsMessenger(string value)
        {
            string head = HeadOf(value);
            return string.Equals(head, "Messenger", StringComparison.OrdinalIgnoreCase)
                || string.Equals(head, "Message", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>전체 Flow 값("Messenger:Seq1:Wait")을 받아 인텐트로 분해. 가족 불일치/시퀀스 누락 = invalid.</summary>
        public static MessengerCommandIntent Parse(string value)
        {
            if (!IsMessenger(value)) return default;

            var parts = value.Split(':');
            if (parts.Length < 2) return default; // 시퀀스 id 누락

            string seqId = parts[1].Trim();
            bool wait = false;
            for (int i = 2; i < parts.Length; i++)
                if (string.Equals(parts[i].Trim(), "Wait", StringComparison.OrdinalIgnoreCase))
                    wait = true;

            return new MessengerCommandIntent(seqId, wait);
        }

        static string HeadOf(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            int ci = value.IndexOf(':');
            return (ci < 0 ? value : value.Substring(0, ci)).Trim();
        }
    }
}
