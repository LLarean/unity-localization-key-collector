#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    [Serializable]
    public class CodeKeyCollector : KeyCollector
    {
        public override string DisplayName       => "Code";
        public override string DefaultOutputPath => "Assets/localization_keys_code.csv";
        public override string CsvLabel          => "Code CSV";

        // Совпадение: "some.key".Localize() или "some.key".LocalizeFast()
        private static readonly Regex RxLiteralKey  = new Regex(
            @"""([^""]+)""\.Localize(?:Fast)?\s*\(", RegexOptions.Compiled);
        private static readonly Regex RxAnyLocalize = new Regex(
            @"\.Localize(?:Fast)?\s*\(", RegexOptions.Compiled);
        private static readonly Regex RxClass       = new Regex(
            @"^\s*(?:(?:public|private|protected|internal|static|abstract|sealed|partial)\s+)*class\s+(\w+)",
            RegexOptions.Compiled);
        private static readonly Regex RxMethod      = new Regex(
            @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|async|new)\s+)*\S+\s+(\w+)\s*[<(]",
            RegexOptions.Compiled);

        public override void DrawOptions()
        {
            Enabled = EditorGUILayout.Toggle("Code (.cs files)", Enabled);
        }

        public override int Collect(
            List<KeyEntry> results, IReadOnlyDictionary<string, string> translations)
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

                ScanFile(absPath, relPath, results, translations);
                scanned++;
            }
            return scanned;
        }

        private void ScanFile(
            string absPath, string relPath,
            List<KeyEntry> results, IReadOnlyDictionary<string, string> translations)
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

                if (!RxAnyLocalize.IsMatch(line)) continue;

                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

                var    keyMatch = RxLiteralKey.Match(line);
                string key      = keyMatch.Success ? keyMatch.Groups[1].Value : "[dynamic]";

                translations.TryGetValue(key, out string translation);
                results.Add(new KeyEntry
                {
                    Key         = key,
                    AssetPath   = relPath,
                    SourceLabel = currentClass,
                    MethodName  = currentMethod,
                    Translation = translation ?? "",
                });
            }
        }

        public override void ExportCsv(List<KeyEntry> entries, string language)
        {
            string header = string.IsNullOrEmpty(language)
                ? "translation"
                : $"translation_{language.ToLower()}";

            var sb = new StringBuilder();
            sb.AppendLine($"key,asset_path,class_name,method_name,{header}");
            foreach (var e in entries)
                sb.AppendLine(
                    $"{CsvExporter.Field(e.Key)}," +
                    $"{CsvExporter.Field(e.AssetPath)}," +
                    $"{CsvExporter.Field(e.SourceLabel)}," +
                    $"{CsvExporter.Field(e.MethodName)}," +
                    $"{CsvExporter.Field(e.Translation)}");

            CsvExporter.WriteFile(OutputPath, sb.ToString());
        }
    }
}
#endif
