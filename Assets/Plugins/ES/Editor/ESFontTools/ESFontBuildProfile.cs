using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ES
{
    public enum ESFontUsage
    {
        Body,
        Title,
        Number,
        Icon,
        Custom,
    }

    public enum ESFontAtlasSize
    {
        Size1024 = 1024,
        Size2048 = 2048,
        Size4096 = 4096,
        Custom = 0,
    }

    [Serializable]
    public sealed class ESFontLanguageBuildEntry
    {
        public string languageCode = "zh-Hans";
        public ESFontUsage usage = ESFontUsage.Body;
        [Tooltip("Optional stable name. Empty uses <Profile>_<Usage>_<Language>.")]
        public string outputName;
        public Font sourceFont;
        [Tooltip("All .txt TextAssets directly under these folders are collected automatically.")]
        public List<DefaultAsset> textFolders = new List<DefaultAsset>();
        [Tooltip("Optional explicit TXT files. Use this only for files outside the configured folders.")]
        public List<TextAsset> textSources = new List<TextAsset>();
        public TMP_FontAsset outputFont;
        [TextArea(2, 4)] public string additionalCharacters;
        [Tooltip("When set, this language/usage uses this chain instead of the profile default.")]
        public List<TMP_FontAsset> fallbackOverride = new List<TMP_FontAsset>();
        [HideInInspector] public string lastInputHash;
    }

    // Editor-only authoring data. Runtime code consumes the generated TMP Font Assets only.
    public sealed class ESFontBuildProfile : ScriptableObject
    {
        public string profileId = "bootstrap";
        [Tooltip("Drop licensed source fonts here. When an entry has no explicit source font, the single font in this folder is used automatically.")]
        public DefaultAsset sourceFontFolder;
        public bool autoUseSingleSourceFont = true;
        public string outputFolder = "Assets/ESNormalAssets/Fonts/Generated";
        public ESFontAtlasSize atlasSize = ESFontAtlasSize.Size2048;
        [Range(8, 256)] public int samplingPointSize = 90;
        [Range(1, 32)] public int atlasPadding = 9;
        public int atlasWidth = 2048;
        public int atlasHeight = 2048;
        public GlyphRenderMode renderMode = GlyphRenderMode.SDFAA;
        public bool enableMultiAtlasSupport = true;
        public List<ESFontLanguageBuildEntry> languages = new List<ESFontLanguageBuildEntry>();
        [Tooltip("Optional Unity Localization String Table Collection assets. They are read through a package-optional adapter and do not add a Localization dependency to ES.")]
        public List<UnityEngine.Object> localizationTableCollections = new List<UnityEngine.Object>();
        public List<TMP_FontAsset> fallbackOrder = new List<TMP_FontAsset>();
        [TextArea(3, 8)] public string lastBuildReport;

        public int AtlasWidth => atlasSize == ESFontAtlasSize.Custom ? atlasWidth : (int)atlasSize;
        public int AtlasHeight => atlasSize == ESFontAtlasSize.Custom ? atlasHeight : (int)atlasSize;
    }
}
