#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    public class LocalizationKeyCollector : EditorWindow
    {
        // ── Sources ───────────────────────────────────────────────────────────────
        private bool _includePrefabs   = true;
        private bool _includeScenes    = true;
        private bool _includeCode      = true;
        private bool _removeDuplicates = false;

        // ── Translation ───────────────────────────────────────────────────────────
        private string _translationLang = "En";

        // ── Output ────────────────────────────────────────────────────────────────
        private string _outputPath     = "Assets/localization_keys.csv";
        private string _outputPathCode = "Assets/localization_keys_code.csv";

        // ── Project-specific settings ─────────────────────────────────────────────
        private string _componentName   = "LocalizedTextMeshPro";
        private string _localizeMethod  = "Localize";
        private string _csvConfigSubdir = "Common/Localization/Configs";
        private string _localAssetPath  = "";

        private bool _showProjectSettings = false;

        // ── Internal ──────────────────────────────────────────────────────────────
        private string  _lastLog;
        private Vector2 _logScrollPos;

        private static readonly Regex RxClass  = new Regex(@"^\s*(?:(?:public|private|protected|internal|static|abstract|sealed|partial)\s+)*class\s+(\w+)",          RegexOptions.Compiled);
        private static readonly Regex RxMethod = new Regex(@"^\s*(?:(?:public|private|protected|internal|static|virtual|override|async|new)\s+)*\S+\s+(\w+)\s*[<(]", RegexOptions.Compiled);

        [MenuItem("Tools/Localization Key Collector")]
        public static void ShowWindow() => GetWindow<LocalizationKeyCollector>("Localization Key Collector");

        private void OnGUI()
        {
            DrawOptionsSection();
            GUILayout.Space(10);
            DrawProjectSettingsSection();
            GUILayout.Space(10);
            DrawOutputSection();
            GUILayout.Space(10);
            DrawCollectButton();
            GUILayout.Space(10);
            DrawLog();
        }

        // ── Options ───────────────────────────────────────────────────────────────

        private void DrawOptionsSection()
        {
            EditorGUILayout.LabelField("Sources", EditorStyles.boldLabel);
            _includePrefabs   = EditorGUILayout.Toggle("Prefabs",          _includePrefabs);
            _includeScenes    = EditorGUILayout.Toggle("Scenes",           _includeScenes);
            _includeCode      = EditorGUILayout.Toggle("Code (.cs files)", _includeCode);

            GUILayout.Space(6);

            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _removeDuplicates = EditorGUILayout.Toggle("Remove duplicates", _removeDuplicates);

            GUILayout.Space(6);

            EditorGUILayout.LabelField("Translation", EditorStyles.boldLabel);
            _translationLang = EditorGUILayout.TextField("Language code", _translationLang);
            EditorGUILayout.HelpBox(
                "Language column from CSV (e.g. En, Ru, De, Fr, Es, Ko, Ja). " +
                "Leave empty to skip translation lookup.",
                MessageType.None);
        }

        // ── Project settings ──────────────────────────────────────────────────────

        private void DrawProjectSettingsSection()
        {
            _showProjectSettings = EditorGUILayout.Foldout(_showProjectSettings, "Project Settings", true, EditorStyles.foldoutHeader);
            if (!_showProjectSettings) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Component", EditorStyles.boldLabel);
            _componentName  = EditorGUILayout.TextField("Localization component", _componentName);
            _localizeMethod = EditorGUILayout.TextField("Localize method name",   _localizeMethod);
            EditorGUILayout.HelpBox(
                "Component: MonoBehaviour class name that holds the localization key.\n" +
                "Method: name used in code to localize strings (e.g. Localize, LocalizeFast).",
                MessageType.None);

            GUILayout.Space(6);

            EditorGUILayout.LabelField("Translation sources", EditorStyles.boldLabel);
            _csvConfigSubdir = EditorGUILayout.TextField("CSV folder (under Assets/)", _csvConfigSubdir);
            _localAssetPath  = EditorGUILayout.TextField("ScriptableObject path",      _localAssetPath);
            EditorGUILayout.HelpBox(
                "CSV folder: path relative to Assets/ where localization CSV files are stored.\n" +
                "ScriptableObject: optional asset path for module-based localization (leave empty to skip).",
                MessageType.None);

            EditorGUI.indentLevel--;
        }

        // ── Output ────────────────────────────────────────────────────────────────

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _outputPath = EditorGUILayout.TextField("Components CSV", _outputPath);
            if (GUILayout.Button("…", GUILayout.Width(24)))
            {
                string picked = EditorUtility.SaveFilePanel("Save CSV", "Assets", "localization_keys", "csv");
                if (!string.IsNullOrEmpty(picked))
                    _outputPath = FileUtil.GetProjectRelativePath(picked);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _outputPathCode = EditorGUILayout.TextField("Code CSV", _outputPathCode);
            if (GUILayout.Button("…", GUILayout.Width(24)))
            {
                string picked = EditorUtility.SaveFilePanel("Save CSV", "Assets", "localization_keys_code", "csv");
                if (!string.IsNullOrEmpty(picked))
                    _outputPathCode = FileUtil.GetProjectRelativePath(picked);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Collect button ────────────────────────────────────────────────────────

        private void DrawCollectButton()
        {
            GUI.enabled = _includePrefabs || _includeScenes || _includeCode;
            if (GUILayout.Button("Collect & Export CSV", GUILayout.Height(30)))
                RunCollection();
            GUI.enabled = true;
        }

        // ── Log ──────────────────────────────────────────────────────────────────

        private void DrawLog()
        {
            if (string.IsNullOrEmpty(_lastLog)) return;

            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            _logScrollPos = EditorGUILayout.BeginScrollView(_logScrollPos, GUILayout.Height(120));
            EditorGUILayout.SelectableLabel(_lastLog, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Copy to clipboard"))
                EditorGUIUtility.systemCopyBuffer = _lastLog;
        }

        // ── Runner ────────────────────────────────────────────────────────────────

        private void RunCollection()
        {
            string lang         = _translationLang.Trim();
            var    translations = string.IsNullOrEmpty(lang)
                ? new Dictionary<string, string>()
                : BuildTranslationLookup(lang, _csvConfigSubdir, _localAssetPath);

            var componentEntries = new List<KeyEntry>();
            var codeEntries      = new List<KeyEntry>();
            int prefabCount      = 0;
            int sceneCount       = 0;
            int codeFiles        = 0;

            string methodEscaped = Regex.Escape(_localizeMethod);
            var rxLiteralKey  = new Regex($@"""([^""]+)""\.{methodEscaped}(?:Fast)?\s*\(", RegexOptions.Compiled);
            var rxAnyLocalize = new Regex($@"\.{methodEscaped}(?:Fast)?\s*\(",             RegexOptions.Compiled);

            try
            {
                if (_includePrefabs) prefabCount = CollectFromPrefabs(componentEntries, translations, _componentName);
                if (_includeScenes)  sceneCount  = CollectFromScenes(componentEntries, translations, _componentName);
                if (_includeCode)    codeFiles   = CollectFromCode(codeEntries, translations, rxLiteralKey, rxAnyLocalize);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (_removeDuplicates)
            {
                componentEntries = RemoveDuplicates(componentEntries);
                codeEntries      = RemoveDuplicates(codeEntries);
            }

            ExportComponentsCsv(componentEntries, _outputPath, lang);
            ExportCodeCsv(codeEntries, _outputPathCode, lang);
            AssetDatabase.Refresh();

            _lastLog =
                $"Component entries: {componentEntries.Count}\n" +
                $"Code entries:      {codeEntries.Count}\n" +
                $"  Prefabs scanned:    {prefabCount}\n" +
                $"  Scenes scanned:     {sceneCount}\n" +
                $"  Code files scanned: {codeFiles}\n" +
                $"  Translations found: {translations.Count}\n" +
                $"Components saved to: {_outputPath}\n" +
                $"Code saved to:       {_outputPathCode}";

            Repaint();
        }

        // ── Duplicates ────────────────────────────────────────────────────────────

        private static List<KeyEntry> RemoveDuplicates(List<KeyEntry> entries)
        {
            var seen   = new HashSet<string>();
            var result = new List<KeyEntry>();
            foreach (var e in entries)
                if (seen.Add(e.Key))
                    result.Add(e);
            return result;
        }

        // ── Translation lookup ────────────────────────────────────────────────────

        private static Dictionary<string, string> BuildTranslationLookup(
            string language, string csvConfigSubdir, string localAssetPath)
        {
            var lookup = new Dictionary<string, string>(System.StringComparer.Ordinal);

            string csvDir = Path.Combine(
                Application.dataPath,
                csvConfigSubdir.Replace('/', Path.DirectorySeparatorChar));

            if (Directory.Exists(csvDir))
            {
                foreach (var csvFile in Directory.GetFiles(csvDir, "*.csv", SearchOption.TopDirectoryOnly))
                {
                    string text;
                    try   { text = File.ReadAllText(csvFile, Encoding.UTF8); }
                    catch { continue; }
                    ParseCsvIntoLookup(text, language, lookup);
                }
            }

            if (!string.IsNullOrEmpty(localAssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(localAssetPath);
                if (asset != null)
                    ReadScriptableObjectLookup(asset, language, lookup);
            }

            return lookup;
        }

        private static void ParseCsvIntoLookup(
            string csvText, string language, Dictionary<string, string> lookup)
        {
            string[] lines = csvText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            if (lines.Length == 0) return;

            string[] header = SplitCsvRow(lines[0]);
            int langCol = -1;
            for (int i = 0; i < header.Length; i++)
            {
                if (string.Equals(header[i].Trim(), language, System.StringComparison.OrdinalIgnoreCase))
                {
                    langCol = i;
                    break;
                }
            }
            if (langCol < 0) return;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = SplitCsvRow(lines[i]);
                if (cols.Length == 0) continue;
                string key = cols[0].Trim();
                if (string.IsNullOrEmpty(key) || key == "Lang") continue;
                if (langCol < cols.Length && !lookup.ContainsKey(key))
                    lookup[key] = cols[langCol];
            }
        }

        private static void ReadScriptableObjectLookup(
            ScriptableObject asset, string language, Dictionary<string, string> lookup)
        {
            string fullLang = CsvCodeToFullLanguageName(language);
            var so      = new SerializedObject(asset);
            var locProp = so.FindProperty("localization");
            if (locProp == null) return;

            var langKeysProp = locProp.FindPropertyRelative("m_keys");
            var langValsProp = locProp.FindPropertyRelative("m_values");
            if (langKeysProp == null || langValsProp == null) return;

            int langIndex = -1;
            for (int i = 0; i < langKeysProp.arraySize; i++)
            {
                if (string.Equals(
                    langKeysProp.GetArrayElementAtIndex(i).stringValue,
                    fullLang,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    langIndex = i;
                    break;
                }
            }
            if (langIndex < 0) return;

            var dictProp = langValsProp.GetArrayElementAtIndex(langIndex);
            var dictKeys = dictProp.FindPropertyRelative("m_keys");
            var dictVals = dictProp.FindPropertyRelative("m_values");
            if (dictKeys == null || dictVals == null) return;

            for (int i = 0; i < dictKeys.arraySize; i++)
            {
                string key = dictKeys.GetArrayElementAtIndex(i).stringValue;
                string val = dictVals.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(key) && !lookup.ContainsKey(key))
                    lookup[key] = val;
            }
        }

        private static string CsvCodeToFullLanguageName(string code) => code.ToUpperInvariant() switch
        {
            "RU" => "Russian",    "EN" => "English",   "DE" => "German",
            "ES" => "Spanish",    "PT" => "Portuguese", "FR" => "French",
            "IT" => "Italian",    "JA" => "Japanese",   "KO" => "Korean",
            "HI" => "Hindi",      "CN" => "Chinese",    "ID" => "Indonesian",
            "AR" => "Arabic",     "MS" => "Malay",      "TR" => "Turkish",
            "VI" => "Vietnamese", "TH" => "Thai",       "SV" => "Swedish",
            "NO" => "Norwegian",  "NI" => "Dutch",      "FI" => "Finnish",
            "DA" => "Danish",     _    => code,
        };

        private static string[] SplitCsvRow(string line)
        {
            var fields = new List<string>();
            int i      = 0;
            while (i <= line.Length)
            {
                if (i == line.Length) { fields.Add(""); break; }

                if (line[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
                            { sb.Append('"'); i += 2; }
                        else if (line[i] == '"')
                            { i++; break; }
                        else
                            sb.Append(line[i++]);
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    int start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    fields.Add(line.Substring(start, i - start));
                    if (i < line.Length && line[i] == ',') i++;
                }
            }
            return fields.ToArray();
        }

        // ── Prefab collection ─────────────────────────────────────────────────────

        private static int CollectFromPrefabs(
            List<KeyEntry> entries, Dictionary<string, string> translations, string componentName)
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
                if (root == null) continue;

                CollectFromGameObject(root, path, entries, translations, componentName);
            }
            return guids.Length;
        }

        // ── Scene collection ──────────────────────────────────────────────────────

        private static int CollectFromScenes(
            List<KeyEntry> entries, Dictionary<string, string> translations, string componentName)
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
                        CollectFromGameObject(root, path, entries, translations, componentName);
                    scanned++;
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return scanned;
        }

        // ── Code collection ───────────────────────────────────────────────────────

        private static int CollectFromCode(
            List<KeyEntry> entries, Dictionary<string, string> translations,
            Regex rxLiteralKey, Regex rxAnyLocalize)
        {
            string   assetsRoot = Application.dataPath;
            string[] csFiles    = Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories);
            int      scanned    = 0;

            for (int i = 0; i < csFiles.Length; i++)
            {
                string absPath = csFiles[i];
                string relPath = "Assets" + absPath.Substring(assetsRoot.Length).Replace('\\', '/');

                if (relPath.Contains("/Editor/"))                    continue;
                if (absPath.EndsWith("LocalizeStub.cs"))             continue;
                if (absPath.EndsWith("LocalizationKeyCollector.cs")) continue;

                EditorUtility.DisplayProgressBar(
                    "Localization Key Collector",
                    $"Code {i + 1}/{csFiles.Length}: {relPath}",
                    (float)i / csFiles.Length);

                ScanCodeFile(absPath, relPath, entries, translations, rxLiteralKey, rxAnyLocalize);
                scanned++;
            }
            return scanned;
        }

        private static void ScanCodeFile(
            string absPath, string relPath,
            List<KeyEntry> entries, Dictionary<string, string> translations,
            Regex rxLiteralKey, Regex rxAnyLocalize)
        {
            string[] lines;
            try   { lines = File.ReadAllLines(absPath, Encoding.UTF8); }
            catch { return; }

            string currentClass  = "";
            string currentMethod = "";

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                var classMatch = RxClass.Match(line);
                if (classMatch.Success)
                {
                    currentClass  = classMatch.Groups[1].Value;
                    currentMethod = "";
                }

                var methodMatch = RxMethod.Match(line);
                if (methodMatch.Success)
                    currentMethod = methodMatch.Groups[1].Value;

                if (!rxAnyLocalize.IsMatch(line)) continue;

                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

                var    keyMatch = rxLiteralKey.Match(line);
                string key      = keyMatch.Success ? keyMatch.Groups[1].Value : "[dynamic]";

                translations.TryGetValue(key, out string translation);

                entries.Add(new KeyEntry
                {
                    Key         = key,
                    AssetPath   = relPath,
                    ClassName   = currentClass,
                    MethodName  = currentMethod,
                    Translation = translation ?? "",
                });
            }
        }

        // ── GameObject scanning ───────────────────────────────────────────────────

        private static void CollectFromGameObject(
            GameObject root, string assetPath,
            List<KeyEntry> entries, Dictionary<string, string> translations,
            string componentName)
        {
            foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;

                string key = ExtractKey(comp, componentName);
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

        // ── Key extraction ────────────────────────────────────────────────────────

        private static string ExtractKey(MonoBehaviour comp, string componentName)
        {
            var type = comp.GetType();

            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                if (t.Name != componentName) continue;
                var prop = type.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                return prop?.GetValue(comp) as string;
            }

            var so = new SerializedObject(comp);
            return (so.FindProperty("key") ?? so.FindProperty("m_Key"))?.stringValue;
        }

        // ── Hierarchy path ────────────────────────────────────────────────────────

        private static string GetHierarchyPath(Transform t)
        {
            var parts = new Stack<string>();
            while (t != null)
            {
                parts.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }

        // ── CSV export ────────────────────────────────────────────────────────────

        private static void ExportComponentsCsv(List<KeyEntry> entries, string outputPath, string language)
        {
            string translationHeader = string.IsNullOrEmpty(language)
                ? "translation"
                : $"translation_{language.ToLower()}";

            var sb = new StringBuilder();
            sb.AppendLine($"key,hierarchy_path,asset_path,{translationHeader}");

            foreach (var e in entries)
                sb.AppendLine(
                    $"{CsvField(e.Key)}," +
                    $"{CsvField(e.HierarchyPath)}," +
                    $"{CsvField(e.AssetPath)}," +
                    $"{CsvField(e.Translation)}");

            WriteFile(outputPath, sb.ToString());
        }

        private static void ExportCodeCsv(List<KeyEntry> entries, string outputPath, string language)
        {
            string translationHeader = string.IsNullOrEmpty(language)
                ? "translation"
                : $"translation_{language.ToLower()}";

            var sb = new StringBuilder();
            sb.AppendLine($"key,asset_path,class_name,method_name,{translationHeader}");

            foreach (var e in entries)
                sb.AppendLine(
                    $"{CsvField(e.Key)}," +
                    $"{CsvField(e.AssetPath)}," +
                    $"{CsvField(e.ClassName)}," +
                    $"{CsvField(e.MethodName)}," +
                    $"{CsvField(e.Translation)}");

            WriteFile(outputPath, sb.ToString());
        }

        private static void WriteFile(string outputPath, string content)
        {
            string absPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath));
            string dir     = Path.GetDirectoryName(absPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(absPath, content, Encoding.UTF8);
        }

        private static string CsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            bool needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n');
            return needsQuoting ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }

        // ── Data ──────────────────────────────────────────────────────────────────

        private struct KeyEntry
        {
            public string Key;
            public string HierarchyPath;
            public string AssetPath;
            public string ClassName;
            public string MethodName;
            public string Translation;
        }
    }
}
#endif
