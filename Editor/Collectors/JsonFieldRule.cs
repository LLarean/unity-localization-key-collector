#if UNITY_EDITOR
using System;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    /// <summary>
    /// Defines a JSON string field to treat as a localization key.
    ///
    /// If ConditionField is non-empty, the key is included only when the enclosing
    /// JSON object contains ConditionField set to ConditionValue.
    ///
    /// Example — include title_key only when "need_localize": true:
    ///   FieldName      = "title_key"
    ///   ConditionField = "need_localize"
    ///   ConditionValue = "true"
    /// </summary>
    [Serializable]
    public class JsonFieldRule
    {
        [SerializeField] public string FieldName      = "";
        [SerializeField] public string ConditionField = "";     // empty = always include
        [SerializeField] public string ConditionValue = "true";

        public bool IsConditional => !string.IsNullOrEmpty(ConditionField);
    }
}
#endif
