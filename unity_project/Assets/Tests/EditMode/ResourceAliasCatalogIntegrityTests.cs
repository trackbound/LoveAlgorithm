using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Story; // ResourceAliasCatalogSO (Narrative asmdef의 ns는 LoveAlgo.Story)

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 별칭 카탈로그 ↔ 실제 Resources 에셋 정합 가드. 카탈로그 id는 곧 로드 경로 키라서
    /// (StageView <c>BG/{id}</c>·StageLayerView <c>CG·SD/{id}</c>) 에셋 리네임 시 카탈로그를 같이
    /// 안 고치면 **스토리 비주얼이 조용히 깨진다**(2026-06-11 BG 한글 리네임에서 실발생 — 이 가드가 재발 차단).
    /// BGM/SFX(AudioClip)도 동일 원리. 빈 섹션은 통과 — 감독 컨벤션(2026-06-11)상 CSV가 에셋명을 직접 쓰면
    /// (미등록=passthrough) 별칭 등록이 불필요하므로, "등록했다면 깨진 등록이 아닐 것"만 강제한다.
    /// Characters는 표정 조합 다수+파일 대소문자 유동이라 제외(대소문자는 에디터 무시·빌드 민감 — 빌드 스모크 영역).
    /// </summary>
    public class ResourceAliasCatalogIntegrityTests
    {
        static ResourceAliasCatalogSO Load()
        {
            var so = Resources.Load<ResourceAliasCatalogSO>("Data/ResourceAliasCatalog");
            Assert.IsNotNull(so, "Resources/Data/ResourceAliasCatalog.asset 존재");
            return so;
        }

        static void AssertAllResolve<T>(System.Collections.Generic.IReadOnlyList<ResourceAliasCatalogSO.Entry> entries,
            string folder, string kind) where T : Object
        {
            if (entries == null) return; // 빈 섹션 허용(직접 에셋명 컨벤션)
            foreach (var e in entries)
            {
                var asset = Resources.Load<T>($"{folder}/{e.id}");
                Assert.IsNotNull(asset,
                    $"{kind} id '{e.id}' → Resources/{folder}/{e.id} 부재 — 에셋 리네임 시 카탈로그 id도 갱신 필요");
            }
        }

        [Test] public void Bg_Ids_All_Resolve_To_Sprites() => AssertAllResolve<Sprite>(Load().Bg, "BG", "BG");
        [Test] public void Cg_Ids_All_Resolve_To_Sprites() => AssertAllResolve<Sprite>(Load().Cg, "CG", "CG");
        [Test] public void Sd_Ids_All_Resolve_To_Sprites() => AssertAllResolve<Sprite>(Load().Sd, "SD", "SD");
        [Test] public void Bgm_Ids_All_Resolve_To_Clips() => AssertAllResolve<AudioClip>(Load().Bgm, "Audio/BGM", "BGM");
        [Test] public void Sfx_Ids_All_Resolve_To_Clips() => AssertAllResolve<AudioClip>(Load().Sfx, "Audio/SFX", "SFX");
    }
}
