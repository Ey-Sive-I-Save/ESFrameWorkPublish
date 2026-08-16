using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TextCore.LowLevel;

namespace ES
{
    public enum ESFontUsage
    {
        [InspectorName("正文")] Body,
        [InspectorName("标题")] Title,
        [InspectorName("数字")] Number,
        [InspectorName("图标")] Icon,
        [InspectorName("自定义")] Custom,
    }

    public enum ESFontAtlasSize
    {
        [InspectorName("1024 × 1024")] Size1024 = 1024,
        [InspectorName("2048 × 2048")] Size2048 = 2048,
        [InspectorName("4096 × 4096")] Size4096 = 4096,
        [InspectorName("自定义")] Custom = 0,
    }

    public enum ESFontGenerationQuality
    {
        [InspectorName("快速预览")] Preview,
        [InspectorName("标准质量")] Standard,
        [InspectorName("高清界面")] HighDefinition,
        [InspectorName("超大字符集")] MassiveCharacterSet,
    }

    public enum ESFontScriptGroup
    {
        [InspectorName("拉丁文字")] Latin,
        [InspectorName("西里尔文字")] Cyrillic,
        [InspectorName("简体中文")] ChineseSimplified,
        [InspectorName("繁体中文")] ChineseTraditional,
        [InspectorName("日文")] Japanese,
        [InspectorName("韩文")] Korean,
        [InspectorName("符号与图标")] Symbols,
    }

    [Serializable]
    public sealed class ESFontRoleSourceOverride
    {
        [Sirenix.OdinInspector.LabelText("字体角色")]
        public ESFontUsage usage = ESFontUsage.Title;
        [Sirenix.OdinInspector.LabelText("源字体文件")]
        public Font sourceFont;
    }

    [Serializable]
    public sealed class ESFontScriptSource
    {
        [Sirenix.OdinInspector.LabelText("文字类型")]
        public ESFontScriptGroup scriptGroup;
        [Sirenix.OdinInspector.LabelText("默认源字体")]
        public Font defaultFont;
        [Sirenix.OdinInspector.LabelText("角色专用字体")]
        public List<ESFontRoleSourceOverride> roleOverrides = new List<ESFontRoleSourceOverride>();
    }

    [Serializable]
    public sealed class ESFontFamilyDefinition
    {
        [Sirenix.OdinInspector.LabelText("字体族 ID")]
        [Tooltip("稳定的 ES 字体族身份，不是 TMP 资产名称。")]
        public string familyId = "game_default";
        [Sirenix.OdinInspector.LabelText("文字类型字体")]
        public List<ESFontScriptSource> sources = new List<ESFontScriptSource>();
    }

    [Serializable]
    public sealed class ESFontLanguageBuildEntry
    {
        [Sirenix.OdinInspector.LabelText("语言")]
        public EnumCollect.Envir_LanguageType language = EnumCollect.Envir_LanguageType.ChineseSimplified;
        [Sirenix.OdinInspector.LabelText("字体角色")]
        public ESFontUsage usage = ESFontUsage.Body;
        [Sirenix.OdinInspector.LabelText("输出名称")]
        [Tooltip("可选的稳定输出名。留空时使用 <方案>_<角色>_<语言>。")]
        public string outputName;
        [Sirenix.OdinInspector.LabelText("文本目录")]
        [Tooltip("自动收集这些目录中的 .txt 文本资产。")]
        public List<DefaultAsset> textFolders = new List<DefaultAsset>();
        [Sirenix.OdinInspector.LabelText("额外文本文件")]
        [Tooltip("可选的独立 TXT 文件，仅用于配置目录之外的文本。")]
        public List<TextAsset> textSources = new List<TextAsset>();
        [HideInInspector]
        public TMP_FontAsset outputFont;
        [Sirenix.OdinInspector.LabelText("补充字符")]
        [TextArea(2, 4)] public string additionalCharacters;
        [HideInInspector] public string lastMissingCharacters;
        [HideInInspector] public string lastInputHash;

        [FormerlySerializedAs("languageCode"), HideInInspector]
        public string legacyLanguageCode;
        [FormerlySerializedAs("sourceFont"), HideInInspector]
        public Font legacySourceFont;
        [FormerlySerializedAs("fallbackOverride"), HideInInspector]
        public List<TMP_FontAsset> legacyFallbackOverride = new List<TMP_FontAsset>();
    }

    // Editor-only authoring data. Runtime code consumes the generated TMP Font Assets only.
    public sealed class ESFontBuildProfile : ScriptableObject
    {
        [Sirenix.OdinInspector.LabelText("方案 ID")]
        public string profileId = "bootstrap";
        [Sirenix.OdinInspector.LabelText("受管源字体目录")]
        [Tooltip("固定为项目内受管字体目录。工具只在用户点击绑定或构建时访问，不在 ReloadDomain 自动扫描。")]
        public DefaultAsset sourceFontFolder;
        [Sirenix.OdinInspector.LabelText("ES 字体族")]
        public ESFontFamilyDefinition fontFamily = new ESFontFamilyDefinition();
        [Sirenix.OdinInspector.LabelText("启用语言")]
        public List<EnumCollect.Envir_LanguageType> enabledLanguages = new List<EnumCollect.Envir_LanguageType>();
        [Sirenix.OdinInspector.LabelText("启用字体角色")]
        public List<ESFontUsage> enabledUsages = new List<ESFontUsage> { ESFontUsage.Body };
        [Sirenix.OdinInspector.LabelText("输出目录")]
        public string outputFolder = "Assets/ESNormalAssets/Fonts/Generated";
        [Sirenix.OdinInspector.LabelText("生成质量")]
        [Tooltip("选择面向开发预览、普通 UI、高清 UI 或超大字符集的 ES 预设，无需配置字体引擎参数。")]
        public ESFontGenerationQuality generationQuality = ESFontGenerationQuality.HighDefinition;
        [HideInInspector]
        public ESFontAtlasSize atlasSize = ESFontAtlasSize.Size2048;
        [HideInInspector]
        [Range(8, 256)] public int samplingPointSize = 90;
        [HideInInspector]
        [Range(1, 32)] public int atlasPadding = 9;
        [HideInInspector]
        public int atlasWidth = 2048;
        [HideInInspector]
        public int atlasHeight = 2048;
        [HideInInspector]
        public GlyphRenderMode renderMode = GlyphRenderMode.SDFAA;
        [HideInInspector]
        public bool enableMultiAtlasSupport = true;
        [HideInInspector]
        public bool blockOnUnresolvedGlyphs = true;
        [Sirenix.OdinInspector.LabelText("语言与角色内容配置")]
        [Tooltip("由“同步十语言方案”维护身份组合；这里只补充文本目录、独立 TXT 和额外字符。")]
        public List<ESFontLanguageBuildEntry> languages = new List<ESFontLanguageBuildEntry>();
        [Sirenix.OdinInspector.LabelText("ES 本地化目录")]
        [Tooltip("ES 原生本地化文本是字体字符收集的一等输入；只收集与字体条目语言匹配的文本。")]
        public List<ESLocalizationCatalog> localizationCatalogs = new List<ESLocalizationCatalog>();
        [HideInInspector]
        public List<UnityEngine.Object> localizationTableCollections = new List<UnityEngine.Object>();
        [HideInInspector]
        public ESRuntimeFontCatalog runtimeCatalog;
        [HideInInspector]
        public bool buildRuntimeCatalog = true;
        [HideInInspector]
        [TextArea(3, 8)] public string lastBuildReport;

        [FormerlySerializedAs("autoUseSingleSourceFont"), HideInInspector]
        public bool legacyAutoUseSingleSourceFont;
        [FormerlySerializedAs("fallbackOrder"), HideInInspector]
        public List<TMP_FontAsset> legacyFallbackOrder = new List<TMP_FontAsset>();

        public int AtlasWidth => generationQuality == ESFontGenerationQuality.Preview ? 1024
            : generationQuality == ESFontGenerationQuality.Standard ? 2048 : 4096;
        public int AtlasHeight => generationQuality == ESFontGenerationQuality.Preview ? 1024
            : generationQuality == ESFontGenerationQuality.Standard ? 2048 : 4096;
        public int SamplingPointSize => generationQuality == ESFontGenerationQuality.Preview ? 64
            : generationQuality == ESFontGenerationQuality.MassiveCharacterSet ? 72 : 90;
        public int AtlasPadding => generationQuality == ESFontGenerationQuality.Preview ? 6 : 9;
        public GlyphRenderMode RenderMode => GlyphRenderMode.SDFAA;
        public bool MultiAtlasSupport => generationQuality != ESFontGenerationQuality.Preview;
    }
}
