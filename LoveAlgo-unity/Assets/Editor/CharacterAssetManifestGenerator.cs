using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Editor
{
    [Serializable]
    public class Manifest
    {
        public int version = 1;
        public string generatedAt;
        public List<CharacterEntry> characters = new();
    }

    [Serializable]
    public class CharacterEntry
    {
        public string id;
        public string displayName;
        public List<EmoteEntry> emotes = new();
    }

    [Serializable]
    public class EmoteEntry
    {
        public string key;
        public string fileName;
        public string resourcePath;
        public string guid;
        public int width;
        public int height;
        public float ppu;
    }

    public static class CharacterAssetManifestGenerator
    {
        const string BaseFolder = "Assets/Resources/Characters";
        const string OutPath = "Assets/Data/characters_emotes.json";

        [MenuItem("LoveAlgo/Tools/Export Character Asset Manifest", priority = 320)]
        public static void ExportManifest()
        {
            var manifest = new Manifest { generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

            // Try to find CharacterDatabase for display names
            CharacterDatabase db = null;
            var guids = AssetDatabase.FindAssets("t:CharacterDatabase");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(path);
            }

            if (!Directory.Exists(BaseFolder))
            {
                Debug.LogWarning($"[CharacterAssetManifestGenerator] Folder not found: {BaseFolder}");
                return;
            }

            var charDirs = Directory.GetDirectories(BaseFolder);
            Array.Sort(charDirs, StringComparer.OrdinalIgnoreCase);

            foreach (var dir in charDirs)
            {
                var charId = Path.GetFileName(dir);
                var entry = new CharacterEntry { id = charId, displayName = db?.CharacterIdToDisplayName(charId) ?? charId };

                var pngs = Directory.GetFiles(dir, "*.png");
                Array.Sort(pngs, StringComparer.OrdinalIgnoreCase);

                foreach (var p in pngs)
                {
                    var assetPath = p.Replace("\\", "/");
                    if (!assetPath.StartsWith("Assets/"))
                    {
                        var idx = assetPath.IndexOf("Assets/");
                        if (idx >= 0) assetPath = assetPath.Substring(idx);
                        else continue;
                    }

                    var key = Path.GetFileNameWithoutExtension(assetPath);
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    int w = tex != null ? tex.width : 0;
                    int h = tex != null ? tex.height : 0;
                    var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    float ppu = ti != null ? ti.spritePixelsPerUnit : 0f;

                    var em = new EmoteEntry
                    {
                        key = key,
                        fileName = Path.GetFileName(assetPath),
                        resourcePath = $"Characters/{charId}/{key}",
                        guid = guid,
                        width = w,
                        height = h,
                        ppu = ppu,
                    };

                    entry.emotes.Add(em);
                }

                manifest.characters.Add(entry);
            }

            // Ensure output folder
            var outDir = Path.GetDirectoryName(OutPath);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            var json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(OutPath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterAssetManifestGenerator] Manifest written: {OutPath}");
            EditorUtility.RevealInFinder(OutPath);
        }
    }
}