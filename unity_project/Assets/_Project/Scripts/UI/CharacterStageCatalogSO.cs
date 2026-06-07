using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 히로인(캐릭터 코드 ID)별 **스테이지 배치** — 슬롯(L/C/R) 기본 위치·크기에 얹는 스케일 배율 + x/y 오프셋을 한 곳에서 관리.
    /// <see cref="StageView"/>가 캐릭터 Enter 시 ID로 조회해 슬롯 Image의 <c>localScale</c>·<c>anchoredPosition</c>에
    /// 적용한다(ADR-007: 데이터=SO, 적용=뷰). 미등록 캐릭터는 항등(<see cref="StagePlacement.Identity"/> — scale 1, offset 0)
    /// 이라 슬롯 기본 그대로. 작가/감독은 인스펙터에서 5명을 한 리스트로 튜닝(코드·씬에 히로인별 분기 0).
    ///
    /// <para>uGUI Image는 표시 크기를 RectTransform이 결정하므로(스프라이트 PPU 무관), "절반으로 줄이기"는
    /// 여기 <see cref="Entry.scale"/>=0.5로. 슬롯 pivot이 발바닥(0.5,0)이라 줄여도 발은 바닥에 고정된다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "LoveAlgo/Character Stage Catalog", fileName = "CharacterStageCatalog")]
    public class CharacterStageCatalogSO : ScriptableObject
    {
        /// <summary>캐릭터 1명의 배치 항목. class라 인스펙터 신규 추가 시 <see cref="scale"/> 기본 1(=원본 크기).</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("캐릭터 코드 ID(예: c01). 슬롯이 아니라 캐릭터 단위 — 같은 히로인은 어느 슬롯에 서든 동일 배치.")]
            public string characterId;
            [Tooltip("슬롯 기본 크기에 곱하는 배율(0.5=절반). 발바닥 pivot 기준이라 발은 바닥 고정.")]
            public float scale = 1f;
            [Tooltip("슬롯 기본 위치에 더하는 x/y 오프셋(px, 1920x1080 기준).")]
            public Vector2 offset;
        }

        [Tooltip("캐릭터 ID별 배치. 미등록 ID는 슬롯 기본 그대로(항등).")]
        [SerializeField] List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>
        /// 캐릭터 ID → 배치(미등록·null·빈 리스트 = 항등). 순수 — 리스트와 ID만으로 결정(GameObject·SO 인스턴스 불필요,
        /// EditMode 테스트 대상). ID 비교는 대소문자 무시(c01==C01).
        /// </summary>
        public static StagePlacement Resolve(IReadOnlyList<Entry> entries, string characterId)
        {
            if (entries != null && !string.IsNullOrEmpty(characterId))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e != null && string.Equals(e.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                        return new StagePlacement(e.scale, e.offset);
                }
            }
            return StagePlacement.Identity;
        }

        /// <summary>인스턴스 조회(내부 리스트 사용).</summary>
        public StagePlacement Resolve(string characterId) => Resolve(entries, characterId);
    }

    /// <summary>해석된 스테이지 배치(스케일 배율 + 오프셋). 슬롯 기본 위에 합성한다. 항등=슬롯 기본 그대로.</summary>
    public readonly struct StagePlacement
    {
        public readonly float Scale;
        public readonly Vector2 Offset;

        public StagePlacement(float scale, Vector2 offset) { Scale = scale; Offset = offset; }

        public static StagePlacement Identity => new StagePlacement(1f, Vector2.zero);

        /// <summary>슬롯 기본 스케일에 배율 적용(순수).</summary>
        public Vector3 ScaleFrom(Vector3 baseScale) => baseScale * Scale;

        /// <summary>슬롯 기본 위치에 오프셋 합성(순수).</summary>
        public Vector2 PositionFrom(Vector2 basePosition) => basePosition + Offset;
    }
}
