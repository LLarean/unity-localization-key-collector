#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    /// <summary>
    /// Shared helper for scanning a GameObject hierarchy for localization keys.
    /// Used by both ComponentKeyCollector (prefabs) and scene scanning.
    /// </summary>
    internal static class GameObjectScanner
    {
        public static void Scan(
            GameObject root, string assetPath,
            List<KeyEntry> entries, IReadOnlyDictionary<string, string> translations)
        {
            foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;

                string key = ExtractKey(comp);
                if (string.IsNullOrEmpty(key)) continue;

                translations.TryGetValue(key, out string translation);
                entries.Add(new KeyEntry
                {
                    Key           = key,
                    HierarchyPath = GetHierarchyPath(comp.transform),
                    AssetPath     = assetPath,
                    Translation   = translation ?? "",
                });
            }
        }

        private static string ExtractKey(MonoBehaviour comp)
        {
            var type = comp.GetType();

            // Support LocalizedTextMeshPro via reflection
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                if (t.Name != "LocalizedTextMeshPro") continue;
                var prop = type.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                return prop?.GetValue(comp) as string;
            }

            // General case: look for serialized fields named key / m_Key
            var so = new SerializedObject(comp);
            return (so.FindProperty("key") ?? so.FindProperty("m_Key"))?.stringValue;
        }

        private static string GetHierarchyPath(Transform t)
        {
            var parts = new Stack<string>();
            while (t != null) { parts.Push(t.name); t = t.parent; }
            return string.Join("/", parts);
        }
    }
}
#endif
