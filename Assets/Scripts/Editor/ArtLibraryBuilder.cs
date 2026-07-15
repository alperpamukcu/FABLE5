using System.Collections.Generic;
using System.IO;
using LastCall.Game;
using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Scans Assets/Art/&lt;Category&gt; and (re)builds Assets/Art/ArtLibrary.asset so the
    /// generated sprites are addressable by id at runtime. Rerun after any asset batch.
    /// </summary>
    public static class ArtLibraryBuilder
    {
        private const string AssetPath = "Assets/Art/ArtLibrary.asset";
        private static readonly string[] Categories =
            { "Ingredients", "Portraits", "VIPs", "Tools", "Icons" };

        [MenuItem("LastCall/Build Art Library")]
        public static void Build()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            int total = 0;
            foreach (var category in Categories)
            {
                var dir = $"Assets/Art/{category}";
                var entries = new List<ArtLibrary.Entry>();
                if (Directory.Exists(dir))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { dir }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (sprite != null)
                            entries.Add(new ArtLibrary.Entry
                            {
                                id = Path.GetFileNameWithoutExtension(path),
                                sprite = sprite
                            });
                    }
                }
                library.SetCategory(category, entries);
                total += entries.Count;
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LastCall] Art library built: {total} sprites across {Categories.Length} categories.");
        }
    }
}
