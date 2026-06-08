using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 공유: 버튼 targetGraphic 스프라이트가 속한 폴더에서 네이밍 규약(<see cref="StyledButtonSpriteConvention"/>)
    /// 의 상태 형제(_hover/_disabled/_on)를 찾아 로드한다. StyledButtonWireTool(→StyledButton 필드)·
    /// ButtonSpriteSwapWireTool(→ButtonSpriteSwap 필드) 공용 — AssetDatabase 열거+로드를 한 곳으로(DRY).
    /// </summary>
    public static class ButtonStateSpriteResolver
    {
        /// <summary>해석된 상태 스프라이트(없으면 null).</summary>
        public readonly struct Resolved
        {
            public readonly Sprite Hover;
            public readonly Sprite Disabled;
            public readonly Sprite On;
            public Resolved(Sprite hover, Sprite disabled, Sprite on) { Hover = hover; Disabled = disabled; On = on; }
            public bool Any => Hover != null || Disabled != null || On != null;
        }

        /// <summary><paramref name="img"/>의 현재 스프라이트 이름에서 같은 폴더의 상태 형제를 해석·로드. 스프라이트 없으면 전부 null.</summary>
        public static Resolved Resolve(Image img)
        {
            if (img == null || img.sprite == null) return default;

            string path = AssetDatabase.GetAssetPath(img.sprite);
            if (string.IsNullOrEmpty(path)) return default; // 빌트인/아틀라스 등 프로젝트 외
            string dir = ToDir(System.IO.Path.GetDirectoryName(path));
            string baseName = StyledButtonSpriteConvention.NormalizeBase(System.IO.Path.GetFileNameWithoutExtension(path));

            // 같은 폴더(직속)의 스프라이트 이름 → 에셋경로.
            var byName = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (ToDir(System.IO.Path.GetDirectoryName(p)) != dir) continue; // 하위 폴더 제외
                byName[System.IO.Path.GetFileNameWithoutExtension(p)] = p;
            }

            var r = StyledButtonSpriteConvention.Resolve(baseName, new HashSet<string>(byName.Keys));
            return new Resolved(Load(byName, r.Highlighted), Load(byName, r.Disabled), Load(byName, r.Selected));
        }

        static Sprite Load(Dictionary<string, string> byName, string name)
            => name != null && byName.TryGetValue(name, out var p) ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;

        static string ToDir(string p) => string.IsNullOrEmpty(p) ? p : p.Replace('\\', '/');
    }
}
