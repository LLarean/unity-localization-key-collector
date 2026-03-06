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
    public class JsonKeyCollector : KeyCollector
    {
        // Default rules matching this project's balance JSON conventions:
        //   description_key, button_key, bubble_key — always localization keys
        //   title_key — localization key only when the same object has "need_localize": true
        [SerializeField] private List<JsonFieldRule> _rules = new List<JsonFieldRule>
        {
            new JsonFieldRule { FieldName = "description_key" },
            new JsonFieldRule { FieldName = "button_key"      },
            new JsonFieldRule { FieldName = "bubble_key"      },
            new JsonFieldRule
            {
                FieldName      = "title_key",
                ConditionField = "need_localize",
                ConditionValue = "true",
            },
        };

        public override string DisplayName       => "JSON";
        public override string DefaultOutputPath => "Assets/localization_keys_json.csv";
        public override string CsvLabel          => "JSON CSV";

        // Matches string fields of the form  "field_name": "value"
        private static readonly Regex RxStringField = new Regex(
            @"""([\w]+)""\s*:\s*""([^""]*)""\s*,?", RegexOptions.Compiled);

        public override void DrawOptions()
        {
            Enabled = EditorGUILayout.Toggle("JSON balance files", Enabled);
            if (!Enabled) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                "Field rules  (field name · condition field · condition value)",
                EditorStyles.miniLabel);

            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                EditorGUILayout.BeginHorizontal();
                rule.FieldName      = EditorGUILayout.TextField(rule.FieldName,      GUILayout.Width(150));
                rule.ConditionField = EditorGUILayout.TextField(rule.ConditionField, GUILayout.Width(110));
                rule.ConditionValue = EditorGUILayout.TextField(rule.ConditionValue, GUILayout.Width(50));
                if (GUILayout.Button("✕", GUILayout.Width(22))) { _rules.RemoveAt(i); i--; }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add rule"))
                _rules.Add(new JsonFieldRule());

            EditorGUI.indentLevel--;
        }

        public override int Collect(
            List<KeyEntry> results, IReadOnlyDictionary<string, string> translations)
        {
            string   assetsRoot = Application.dataPath;
            string[] jsonFiles  = Directory.GetFiles(assetsRoot, "*.json", SearchOption.AllDirectories);
            int      scanned    = 0;

            for (int i = 0; i < jsonFiles.Length; i++)
            {
                string absPath = jsonFiles[i];
                string relPath = "Assets" + absPath.Substring(assetsRoot.Length).Replace('\\', '/');

                if (relPath.Contains("/Editor/")) continue;

                EditorUtility.DisplayProgressBar(
                    "Localization Key Collector",
                    $"JSON {i + 1}/{jsonFiles.Length}: {relPath}",
                    (float)i / jsonFiles.Length);

                ScanFile(absPath, relPath, results, translations);
                scanned++;
            }
            return scanned;
        }

        private void ScanFile(
            string absPath, string relPath,
            List<KeyEntry> results, IReadOnlyDictionary<string, string> translations)
        {
            string text;
            try   { text = File.ReadAllText(absPath, Encoding.UTF8); }
            catch { return; }

            foreach (Match m in RxStringField.Matches(text))
            {
                string field = m.Groups[1].Value;
                string key   = m.Groups[2].Value;
                if (string.IsNullOrEmpty(key)) continue;

                var rule = FindRule(field);
                if (rule == null) continue;

                if (rule.IsConditional &&
                    !EnclosingObjectSatisfiesCondition(text, m.Index, rule.ConditionField, rule.ConditionValue))
                    continue;

                translations.TryGetValue(key, out string translation);
                results.Add(new KeyEntry
                {
                    Key         = key,
                    SourceLabel = field,
                    AssetPath   = relPath,
                    Translation = translation ?? "",
                });
            }
        }

        private JsonFieldRule FindRule(string fieldName)
        {
            foreach (var rule in _rules)
                if (!string.IsNullOrEmpty(rule.FieldName) && rule.FieldName == fieldName)
                    return rule;
            return null;
        }

        // Checks whether the JSON object enclosing matchPos contains
        // conditionField set to conditionValue.
        private static bool EnclosingObjectSatisfiesCondition(
            string text, int matchPos, string conditionField, string conditionValue)
        {
            // Walk backwards to find the opening '{' of the enclosing object
            int depth    = 0;
            int objStart = -1;
            for (int i = matchPos - 1; i >= 0; i--)
            {
                char c = text[i];
                if      (c == '}') depth++;
                else if (c == '{') { if (depth == 0) { objStart = i; break; } depth--; }
            }
            if (objStart < 0) return false;

            // Walk forwards to find the matching '}'
            depth = 0;
            int objEnd = -1;
            for (int i = objStart; i < text.Length; i++)
            {
                char c = text[i];
                if      (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) { objEnd = i; break; } }
            }
            if (objEnd < 0) return false;

            string objText = text.Substring(objStart, objEnd - objStart + 1);

            // Build regex for the specific field+value pair to support any combination
            var rx = new Regex(
                $@"""{Regex.Escape(conditionField)}""\s*:\s*{Regex.Escape(conditionValue)}");
            return rx.IsMatch(objText);
        }

        public override void ExportCsv(List<KeyEntry> entries, string language)
        {
            string header = string.IsNullOrEmpty(language)
                ? "translation"
                : $"translation_{language.ToLower()}";

            var sb = new StringBuilder();
            sb.AppendLine($"key,json_field,asset_path,{header}");
            foreach (var e in entries)
                sb.AppendLine(
                    $"{CsvExporter.Field(e.Key)}," +
                    $"{CsvExporter.Field(e.SourceLabel)}," +
                    $"{CsvExporter.Field(e.AssetPath)}," +
                    $"{CsvExporter.Field(e.Translation)}");

            CsvExporter.WriteFile(OutputPath, sb.ToString());
        }
    }
}
#endif
