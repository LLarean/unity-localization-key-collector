#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    public static class CsvExporter
    {
        public static void WriteFile(string outputPath, string content)
        {
            string absPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath));
            string dir     = Path.GetDirectoryName(absPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(absPath, content, Encoding.UTF8);
        }

        public static string Field(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            bool needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n');
            return needsQuoting ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }
    }
}
#endif
