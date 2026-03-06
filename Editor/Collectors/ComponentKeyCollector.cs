#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    [Serializable]
    public class ComponentKeyCollector : KeyCollector
    {
        [SerializeField] private bool _includePrefabs = true;
        [SerializeField] private bool _includeScenes  = true;

        public override string DisplayName       => "Components";
        public override string DefaultOutputPath => "Assets/localization_keys.csv";
        public override string CsvLabel          => "Components CSV";

        public override void DrawOptions()
        {
            Enabled = EditorGUILayout.Toggle("Components", Enabled);
            if (!Enabled) return;

            EditorGUI.indentLevel++;
            _includePrefabs = EditorGUILayout.Toggle("Prefabs", _includePrefabs);
            _includeScenes  = EditorGUILayout.Toggle("Scenes",  _includeScenes);
            EditorGUI.indentLevel--;
        }

        public override int Collect(
            List<KeyEntry> results, IReadOnlyDictionary<string, string> translations)
        {
            int count = 0;
            if (_includePrefabs) count += CollectPrefabs(results, translations);
            if (_includeScenes)  count += CollectScenes(results, translations);
            return count;
        }

        private static int CollectPrefabs(
            List<KeyEntry> results, IReadOnlyDictionary<string, string> translations)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.StartsWith("Assets/")) continue;

                EditorUtility.DisplayProgressBar(
                    "Localization Key Collector",
                    $"Prefab {i + 1}/{guids.Length}: {path}",
                    (float)i / guids.Length);

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) GameObjectScanner.Scan(root, path, results, translations);
            }
            return guids.Length;
        }

        private static int CollectScenes(
            List<KeyEntry> results, IReadOnlyDictionary<string, string> translations)
        {
            string[] guids   = AssetDatabase.FindAssets("t:Scene");
            int      scanned = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.StartsWith("Assets/")) continue;

                EditorUtility.DisplayProgressBar(
                    "Localization Key Collector",
                    $"Scene {i + 1}/{guids.Length}: {path}",
                    (float)i / guids.Length);

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        GameObjectScanner.Scan(root, path, results, translations);
                    scanned++;
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return scanned;
        }

        public override void ExportCsv(List<KeyEntry> entries, string language)
        {
            string header = string.IsNullOrEmpty(language)
                ? "translation"
                : $"translation_{language.ToLower()}";

            var sb = new StringBuilder();
            sb.AppendLine($"key,hierarchy_path,asset_path,{header}");
            foreach (var e in entries)
                sb.AppendLine(
                    $"{CsvExporter.Field(e.Key)}," +
                    $"{CsvExporter.Field(e.HierarchyPath)}," +
                    $"{CsvExporter.Field(e.AssetPath)}," +
                    $"{CsvExporter.Field(e.Translation)}");

            CsvExporter.WriteFile(OutputPath, sb.ToString());
        }
    }
}
#endif
