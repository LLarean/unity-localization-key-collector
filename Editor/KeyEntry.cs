#if UNITY_EDITOR
namespace LLarean.LocalizationKeyCollector
{
    public struct KeyEntry
    {
        public string Key;
        public string HierarchyPath; // prefab/scene: path to component in hierarchy
        public string AssetPath;
        public string SourceLabel;   // code: class name; JSON: json field name
        public string MethodName;    // code: method name
        public string Translation;
    }
}
#endif
