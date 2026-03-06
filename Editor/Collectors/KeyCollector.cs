#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LLarean.LocalizationKeyCollector
{
    /// <summary>
    /// Base class for all localization key sources.
    /// Each subclass is responsible for one source type (components, code, JSON, etc.),
    /// draws its own options in the Editor window, and exports its own CSV.
    /// </summary>
    [Serializable]
    public abstract class KeyCollector
    {
        [SerializeField] protected bool   enabled    = true;
        [SerializeField] protected string outputPath = "";

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        /// <summary>
        /// Export path. Falls back to DefaultOutputPath when empty.
        /// </summary>
        public string OutputPath
        {
            get => string.IsNullOrEmpty(outputPath) ? DefaultOutputPath : outputPath;
            set => outputPath = value;
        }

        /// <summary>Display name shown in the window.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Default CSV output path.</summary>
        public abstract string DefaultOutputPath { get; }

        /// <summary>Label for the Output row in the window.</summary>
        public abstract string CsvLabel { get; }

        /// <summary>Draws the source toggle and any collector-specific options.</summary>
        public abstract void DrawOptions();

        /// <summary>
        /// Collects localization key entries.
        /// Returns the number of assets/files scanned.
        /// </summary>
        public abstract int Collect(
            List<KeyEntry> results,
            IReadOnlyDictionary<string, string> translations);

        /// <summary>Exports collected entries to CSV at OutputPath.</summary>
        public abstract void ExportCsv(List<KeyEntry> entries, string language);
    }
}
#endif
