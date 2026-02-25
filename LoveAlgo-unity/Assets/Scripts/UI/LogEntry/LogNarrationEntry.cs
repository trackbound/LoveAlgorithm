using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 독백 로그 엔트리 — 이름 없이 텍스트만
    /// 프리팹에 버블만 있고 헤더 영역 없음
    /// </summary>
    public class LogNarrationEntry : LogEntryBase
    {
        public override void Init(string speaker, Sprite portrait)
        {
            // 독백은 이름/초상화 없음 — 별도 설정 불필요
        }
    }
}
