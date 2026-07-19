using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>
    /// Runtime id→sprite registry for the generated art (Assets/Art). ScriptableObjects
    /// cannot serialize dictionaries, so each category is a flat list of entries built
    /// by the <c>LastCall → Build Art Library</c> editor tool and cached into lookups on
    /// first access. Missing ids return null so the UI can fall back gracefully.
    /// </summary>
    [CreateAssetMenu(menuName = "LastCall/Art Library", fileName = "ArtLibrary")]
    public sealed class ArtLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private List<Entry> portraits = new List<Entry>();
        [SerializeField] private List<Entry> vips = new List<Entry>();
        [SerializeField] private List<Entry> tools = new List<Entry>();
        [SerializeField] private List<Entry> icons = new List<Entry>();

        private Dictionary<string, Sprite> _portraits, _vips, _tools, _icons;

        public Sprite Portrait(string id) => Lookup(ref _portraits, portraits, id);
        public Sprite Vip(string id) => Lookup(ref _vips, vips, id);
        public Sprite Tool(string id) => Lookup(ref _tools, tools, id);
        public Sprite Icon(string id) => Lookup(ref _icons, icons, id);

        private static Sprite Lookup(ref Dictionary<string, Sprite> cache, List<Entry> entries, string id)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Sprite>(entries.Count);
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.id)) cache[e.id] = e.sprite;
            }
            return id != null && cache.TryGetValue(id, out var sprite) ? sprite : null;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: repopulate a category. Clears the runtime cache.</summary>
        public void SetCategory(string category, List<Entry> values)
        {
            switch (category)
            {
                case "Portraits": portraits = values; _portraits = null; break;
                case "VIPs": vips = values; _vips = null; break;
                case "Tools": tools = values; _tools = null; break;
                case "Icons": icons = values; _icons = null; break;
            }
        }
#endif
    }
}
