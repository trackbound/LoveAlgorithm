using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.MessageStack
{
    /// <summary>
    /// 연출용 간이 메시지 스택에 흘려보낼 대사 묶음 Definition SO. CSV 파싱 없이 인스펙터에 몇 줄 박아두면
    /// <see cref="MessageStackController"/>가 자동 타이머로 한 줄씩 발사한다(런타임 읽기 전용).
    /// 값 출처 = 연출 작가 직접 입력(예: ROA 얀데레 텍스트 "어디야?" / "왜 안 와…" / "보고 싶어!").
    /// </summary>
    [CreateAssetMenu(fileName = "MessageSequence", menuName = "LoveAlgo/Message Stack/Sequence")]
    public class MessageSequenceSO : ScriptableObject
    {
        /// <summary>발사할 대사 한 줄 + 등장 전 대기 시간. class라 신규 항목이 delay 기본값을 갖는다.</summary>
        [Serializable]
        public class Line
        {
            [TextArea(1, 3)]
            [Tooltip("표시할 대사 한 줄.")]
            public string text;

            [Tooltip("이 줄이 등장하기 전 대기 시간(초).")]
            public float delay = 1.2f;
        }

        [Tooltip("카드 발신자 이름. 'Message from {sender}'로 표시.")]
        [SerializeField] string senderName = "ROA";

        [Tooltip("첫 줄 등장 전 대기 시간(초).")]
        [SerializeField] float startDelay = 0.5f;

        [Tooltip("순서대로 발사할 대사 줄 목록.")]
        [SerializeField] List<Line> lines = new();

        public string SenderName => senderName;
        public float StartDelay => startDelay;
        public IReadOnlyList<Line> Lines => lines;
    }
}
