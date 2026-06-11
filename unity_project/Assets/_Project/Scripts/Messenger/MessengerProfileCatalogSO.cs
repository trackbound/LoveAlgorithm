using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 플레이어 프로필 커스터마이즈 후보(정의 SO) — 프로필 사진/배경 각 4~5종(기획서).
    /// 세이브는 인덱스만 보유(GameStateData.messengerProfileImage/Bg)하므로 목록 순서가 계약 —
    /// 기존 항목 사이에 끼워 넣지 말고 끝에 추가한다(인덱스 보존).
    /// </summary>
    [CreateAssetMenu(fileName = "MessengerProfileCatalog", menuName = "LoveAlgo/Messenger Profile Catalog")]
    public class MessengerProfileCatalogSO : ScriptableObject
    {
        [SerializeField] List<Sprite> profileImages = new();
        [SerializeField] List<Sprite> backgrounds = new();

        public IReadOnlyList<Sprite> ProfileImages => profileImages;
        public IReadOnlyList<Sprite> Backgrounds => backgrounds;

        /// <summary>후보 일괄 설정(테스트/빌더 주입용 — 인스펙터 편집과 동치).</summary>
        public void SetData(List<Sprite> images, List<Sprite> bgs)
        {
            profileImages = images ?? new List<Sprite>();
            backgrounds = bgs ?? new List<Sprite>();
        }

        /// <summary>인덱스의 프로필 사진. 목록이 비면 null, 범위 밖이면 마지막(후보 축소에도 세이브 안전).</summary>
        public Sprite ProfileImage(int index) => At(profileImages, index);

        /// <summary>인덱스의 배경. 클램프 규칙 동일.</summary>
        public Sprite Background(int index) => At(backgrounds, index);

        static Sprite At(List<Sprite> list, int index)
        {
            if (list == null || list.Count == 0) return null;
            if (index < 0) index = 0;
            if (index >= list.Count) index = list.Count - 1;
            return list[index];
        }
    }
}
