using System;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 슬롯에 직렬화되는 세이브 한 건(JSON). 마이그레이션을 위해 <see cref="version"/> 포함.
    /// 현재는 코어 상태만 — 스크립트 위치/스테이지 스냅샷/모듈 데이터는 해당 시스템 재작성 시 확장.
    /// 썸네일은 별도 이미지 파일로 저장하고 여기엔 파일명만 보유(JSON 비대화 방지).
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAtUtc = "";   // ISO 8601 (DateTime은 JsonUtility 비호환이라 문자열로)
        public string chapterLabel = ""; // 슬롯 표시용

        public GameStateData state = new();

        // 썸네일 이미지 파일명(같은 폴더). 비어 있으면 썸네일 없음.
        public string thumbnailFile = "";
    }
}
