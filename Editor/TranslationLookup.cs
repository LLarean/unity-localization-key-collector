#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    public static class TranslationLookup
    {
        private const string CsvConfigSubdir = "Common/Localization/Configs";
        private const string LocalAssetPath  = "Assets/Modules/Localization/LocalLocalization.asset";

        public static Dictionary<string, string> Build(string language)
        {
            var lookup = new Dictionary<string, string>(System.StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(language)) return lookup;

            string csvDir = Path.Combine(
                Application.dataPath,
                CsvConfigSubdir.Replace('/', Path.DirectorySeparatorChar));

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

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(LocalAssetPath);
            if (asset != null)
                ReadScriptableObjectLookup(asset, language, lookup);

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
            string fullLang = CodeToFullLanguageName(language);

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
                    fullLang, System.StringComparison.OrdinalIgnoreCase))
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

        private static string CodeToFullLanguageName(string code) => code.ToUpperInvariant() switch
        {
            "RU" => "Russian",    "EN" => "English",    "DE" => "German",
            "ES" => "Spanish",    "PT" => "Portuguese", "FR" => "French",
            "IT" => "Italian",    "JA" => "Japanese",   "KO" => "Korean",
            "HI" => "Hindi",      "CN" => "Chinese",    "ID" => "Indonesian",
            "AR" => "Arabic",     "MS" => "Malay",      "TR" => "Turkish",
            "VI" => "Vietnamese", "TH" => "Thai",       "SV" => "Swedish",
            "NO" => "Norwegian",  "NI" => "Dutch",      "FI" => "Finnish",
            "DA" => "Danish",     _    => code,
        };

        internal static string[] SplitCsvRow(string line)
        {
            var fields = new List<string>();
            int i = 0;
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
    }
}
#endif
