using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Story;
using LoveAlgo.Story.Data;

namespace LoveAlgo.Story.EditorTools
{
    /// <summary>
    /// StoryMappings(static dict) → ResourceCatalogSO 마이그레이션.
    /// schema v2: Id(영문 영구) + Aliases[](한글 변경 가능).
    ///
    /// 같은 영문 Id에 매핑되는 한글 키 여러 개는 같은 엔트리의 Aliases에 통합.
    /// 예: StoryMappings.Emote에 "눈웃음→_11", "EyeSmile→_11" 둘 다 있으면
    ///     하나의 EmoteEntry { Id="_11", Aliases=["눈웃음", "EyeSmile"] }로 통합.
    ///
    /// 메뉴:
    ///   Tools > Story > Resource Catalog > Create Default Catalog
    ///   Tools > Story > Resource Catalog > Migrate from StoryMappings
    ///   Tools > Story > Resource Catalog > Validate
    /// </summary>
    public static class ResourceCatalogMigrator
    {
        const string CatalogPath = "Assets/Resources/ResourceCatalog.asset";

        [MenuItem("Tools/Story/Resource Catalog/Create Default Catalog")]
        public static void CreateDefaultCatalog()
        {
            if (File.Exists(CatalogPath))
            {
                Debug.LogWarning($"[ResourceCatalogMigrator] 이미 존재: {CatalogPath}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<ResourceCatalogSO>(CatalogPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var so = ScriptableObject.CreateInstance<ResourceCatalogSO>();
            AssetDatabase.CreateAsset(so, CatalogPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = so;
            Debug.Log($"[ResourceCatalogMigrator] 생성: {CatalogPath}");
        }

        [MenuItem("Tools/Story/Resource Catalog/Migrate from StoryMappings")]
        public static void Migrate()
        {
            var so = AssetDatabase.LoadAssetAtPath<ResourceCatalogSO>(CatalogPath);
            if (so == null)
            {
                Debug.LogError($"[ResourceCatalogMigrator] {CatalogPath} 없음 — 'Create Default Catalog' 먼저");
                return;
            }

            int added = 0, bound = 0, missing = 0, aliasesAdded = 0;

            // ── BG ──
            foreach (var kv in StoryMappings.BG)
            {
                AddOrMergeSprite(so.BG, kv.Value, kv.Key, $"BG/{kv.Value}",
                    ref added, ref bound, ref missing, ref aliasesAdded);
            }

            // ── CG ──
            foreach (var kv in StoryMappings.CG)
            {
                AddOrMergeSprite(so.CG, kv.Value, kv.Key, $"CG/{kv.Value}",
                    ref added, ref bound, ref missing, ref aliasesAdded);
            }

            // ── SD ──
            foreach (var kv in StoryMappings.SD)
            {
                AddOrMergeSprite(so.SD, kv.Value, kv.Key, $"SD/{kv.Value}",
                    ref added, ref bound, ref missing, ref aliasesAdded);
            }

            // ── BGM ──
            foreach (var kv in StoryMappings.BGM)
            {
                AddOrMergeAudio(so.BGM, kv.Value, kv.Key, $"Audio/BGM/{kv.Value}",
                    ref added, ref bound, ref missing, ref aliasesAdded);
            }

            // ── SFX ──
            foreach (var kv in StoryMappings.SFX)
            {
                AddOrMergeAudio(so.SFX, kv.Value, kv.Key, $"Audio/SFX/{kv.Value}",
                    ref added, ref bound, ref missing, ref aliasesAdded);
            }

            // ── Emotes ── (같은 코드 = 같은 Id, 별칭 통합)
            foreach (var kv in StoryMappings.Emote)
            {
                var existing = so.Emotes.FirstOrDefault(e =>
                    e != null && string.Equals(e.Id, kv.Value, System.StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    so.Emotes.Add(new ResourceCatalogSO.EmoteEntry
                    {
                        Id = kv.Value,
                        Aliases = new[] { kv.Key },
                    });
                    added++;
                }
                else
                {
                    // 별칭 추가 (중복 방지)
                    var list = existing.Aliases?.ToList() ?? new List<string>();
                    if (!list.Any(a => string.Equals(a, kv.Key, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(kv.Key);
                        existing.Aliases = list.ToArray();
                        aliasesAdded++;
                    }
                }
            }

            // ── Characters ──
            foreach (var c in StoryMappings.Characters)
            {
                if (so.Characters.Any(e => e != null &&
                    string.Equals(e.Id, c.Id, System.StringComparison.OrdinalIgnoreCase))) continue;
                so.Characters.Add(new ResourceCatalogSO.CharacterEntry
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    Aliases = c.Aliases ?? System.Array.Empty<string>(),
                });
                added++;
            }

            // ── Overlays (자동 스캔 — Resources/Overlay/*.png) ──
            string overlayResRoot = "Assets/Resources/Overlay";
            if (Directory.Exists(overlayResRoot))
            {
                var assets = AssetDatabase.FindAssets("t:Sprite", new[] { overlayResRoot });
                foreach (var guid in assets)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (so.Overlays.Any(e => e != null &&
                        string.Equals(e.Id, name, System.StringComparison.OrdinalIgnoreCase))) continue;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    so.Overlays.Add(new ResourceCatalogSO.SpriteEntry
                    {
                        Id = name,
                        Aliases = System.Array.Empty<string>(),
                        Sprite = sprite,
                        Note = "auto-scan",
                    });
                    added++;
                    if (sprite != null) bound++; else missing++;
                }
            }

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ResourceCatalogSO.ResetInstance();

            string msg = $"[ResourceCatalogMigrator] 완료 (schema v2: Id + Aliases)\n" +
                         $"  신규 엔트리: {added}개\n" +
                         $"  에셋 바인딩 성공: {bound}개\n" +
                         $"  에셋 미발견: {missing}개 (Inspector에서 수동 할당)\n" +
                         $"  기존 엔트리에 별칭 추가: {aliasesAdded}개";
            Debug.Log(msg);
            EditorUtility.DisplayDialog("Resource Catalog 마이그레이션", msg, "확인");
            Selection.activeObject = so;
        }

        /// <summary>같은 Id 있으면 Aliases에 한글 추가, 없으면 새 엔트리 추가.</summary>
        static void AddOrMergeSprite(List<ResourceCatalogSO.SpriteEntry> list,
            string id, string aliasKo, string resPath,
            ref int added, ref int bound, ref int missing, ref int aliasesAdded)
        {
            var existing = list.FirstOrDefault(e => e != null &&
                string.Equals(e.Id, id, System.StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                var sprite = Resources.Load<Sprite>(resPath);
                list.Add(new ResourceCatalogSO.SpriteEntry
                {
                    Id = id,
                    Aliases = new[] { aliasKo },
                    Sprite = sprite,
                });
                added++;
                if (sprite != null) bound++; else missing++;
            }
            else
            {
                var aliases = existing.Aliases?.ToList() ?? new List<string>();
                if (!aliases.Any(a => string.Equals(a, aliasKo, System.StringComparison.OrdinalIgnoreCase)))
                {
                    aliases.Add(aliasKo);
                    existing.Aliases = aliases.ToArray();
                    aliasesAdded++;
                }
            }
        }

        static void AddOrMergeAudio(List<ResourceCatalogSO.AudioEntry> list,
            string id, string aliasKo, string resPath,
            ref int added, ref int bound, ref int missing, ref int aliasesAdded)
        {
            var existing = list.FirstOrDefault(e => e != null &&
                string.Equals(e.Id, id, System.StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                var clip = Resources.Load<AudioClip>(resPath);
                list.Add(new ResourceCatalogSO.AudioEntry
                {
                    Id = id,
                    Aliases = new[] { aliasKo },
                    Clip = clip,
                });
                added++;
                if (clip != null) bound++; else missing++;
            }
            else
            {
                var aliases = existing.Aliases?.ToList() ?? new List<string>();
                if (!aliases.Any(a => string.Equals(a, aliasKo, System.StringComparison.OrdinalIgnoreCase)))
                {
                    aliases.Add(aliasKo);
                    existing.Aliases = aliases.ToArray();
                    aliasesAdded++;
                }
            }
        }

        [MenuItem("Tools/Story/Resource Catalog/Validate")]
        public static void ValidateCatalog()
        {
            var so = AssetDatabase.LoadAssetAtPath<ResourceCatalogSO>(CatalogPath);
            if (so == null)
            {
                Debug.LogError($"[ResourceCatalogMigrator] {CatalogPath} 없음");
                return;
            }
            var issues = so.Validate();
            if (issues.Count == 0)
                Debug.Log("[ResourceCatalog] ✓ 모든 검증 통과");
            else
                Debug.LogWarning($"[ResourceCatalog] 검증 문제 {issues.Count}개:\n  " + string.Join("\n  ", issues));
        }
    }
}
