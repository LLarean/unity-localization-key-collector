#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    public class LocalizationKeyCollector : EditorWindow
    {
        // ── Collectors ────────────────────────────────────────────────────────────

        [SerializeField] private ComponentKeyCollector _componentCollector = new ComponentKeyCollector();
        [SerializeField] private CodeKeyCollector      _codeCollector      = new CodeKeyCollector();
        [SerializeField] private JsonKeyCollector      _jsonCollector      = new JsonKeyCollector();

        private KeyCollector[] AllCollectors =>
            new KeyCollector[] { _componentCollector, _codeCollector, _jsonCollector };

        // ── Global options ────────────────────────────────────────────────────────

        [SerializeField] private bool   _removeDuplicates  = false;
        [SerializeField] private string _translationLang   = "En";
        [SerializeField] private string _csvConfigSubdir   = TranslationLookup.DefaultCsvConfigSubdir;
        [SerializeField] private string _localAssetPath    = TranslationLookup.DefaultLocalAssetPath;

        // ── State ─────────────────────────────────────────────────────────────────

        private string  _lastLog;
        private Vector2 _logScrollPos;

        // ── Entry point ───────────────────────────────────────────────────────────

        [MenuItem("Tools/Localization Key Collector")]
        public static void ShowWindow() =>
            GetWindow<LocalizationKeyCollector>("Localization Key Collector");

        private void OnGUI()
        {
            DrawSourcesSection();
            GUILayout.Space(8);
            DrawGlobalOptions();
            GUILayout.Space(8);
            DrawOutputSection();
            GUILayout.Space(10);
            DrawCollectButton();
            GUILayout.Space(10);
            DrawLog();
        }

        // ── Sources ───────────────────────────────────────────────────────────────

        private void DrawSourcesSection()
        {
            EditorGUILayout.LabelField("Sources", EditorStyles.boldLabel);
            foreach (var collector in AllCollectors)
                collector.DrawOptions();
        }

        // ── Global options ────────────────────────────────────────────────────────

        private void DrawGlobalOptions()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _removeDuplicates = EditorGUILayout.Toggle("Remove duplicates", _removeDuplicates);

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Translation", EditorStyles.boldLabel);
            _translationLang = EditorGUILayout.TextField("Language code", _translationLang);
            EditorGUILayout.HelpBox(
                "Language column from CSV (e.g. En, Ru, De, Fr, Es, Ko, Ja). " +
                "Leave empty to skip translation lookup.",
                MessageType.None);

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Localization sources", EditorStyles.boldLabel);
            _csvConfigSubdir = EditorGUILayout.TextField("CSV config subdir", _csvConfigSubdir);
            _localAssetPath  = EditorGUILayout.TextField("Local asset path",  _localAssetPath);
            EditorGUILayout.HelpBox(
                "CSV config subdir: path relative to Assets/ containing localization .csv files.\n" +
                "Local asset path: asset path to a ScriptableObject with inline localization data.",
                MessageType.None);
        }

        // ── Output ────────────────────────────────────────────────────────────────

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            foreach (var collector in AllCollectors)
                DrawOutputRow(collector);
        }

        private void DrawOutputRow(KeyCollector collector)
        {
            EditorGUILayout.BeginHorizontal();
            collector.OutputPath = EditorGUILayout.TextField(collector.CsvLabel, collector.OutputPath);
            if (GUILayout.Button("…", GUILayout.Width(24)))
            {
                string picked = EditorUtility.SaveFilePanel(
                    "Save CSV", "Assets",
                    System.IO.Path.GetFileNameWithoutExtension(collector.OutputPath),
                    "csv");
                if (!string.IsNullOrEmpty(picked))
                    collector.OutputPath = FileUtil.GetProjectRelativePath(picked);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Collect ───────────────────────────────────────────────────────────────

        private void DrawCollectButton()
        {
            bool anyEnabled = false;
            foreach (var c in AllCollectors) if (c.Enabled) { anyEnabled = true; break; }

            GUI.enabled = anyEnabled;
            if (GUILayout.Button("Collect & Export CSV", GUILayout.Height(30)))
                RunCollection();
            GUI.enabled = true;
        }

        private void RunCollection()
        {
            string lang         = _translationLang.Trim();
            var    translations = TranslationLookup.Build(lang, _csvConfigSubdir, _localAssetPath);
            var    log          = new StringBuilder();

            try
            {
                foreach (var collector in AllCollectors)
                {
                    if (!collector.Enabled) continue;

                    var entries = new List<KeyEntry>();
                    int scanned = collector.Collect(entries, translations);

                    if (_removeDuplicates)
                        entries = RemoveDuplicates(entries);

                    collector.ExportCsv(entries, lang);

                    log.AppendLine(
                        $"{collector.CsvLabel}: {entries.Count} keys " +
                        $"({scanned} assets scanned) → {collector.OutputPath}");
                }

                log.AppendLine($"Translations available: {translations.Count}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            _lastLog = log.ToString().TrimEnd();
            Repaint();
        }

        // ── Log ───────────────────────────────────────────────────────────────────

        private void DrawLog()
        {
            if (string.IsNullOrEmpty(_lastLog)) return;

            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            _logScrollPos = EditorGUILayout.BeginScrollView(_logScrollPos, GUILayout.Height(160));
            EditorGUILayout.SelectableLabel(
                _lastLog, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Copy to clipboard"))
                EditorGUIUtility.systemCopyBuffer = _lastLog;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static List<KeyEntry> RemoveDuplicates(List<KeyEntry> entries)
        {
            var seen   = new HashSet<string>();
            var result = new List<KeyEntry>();
            foreach (var e in entries)
                if (seen.Add(e.Key)) result.Add(e);
            return result;
        }
    }
}
#endif
