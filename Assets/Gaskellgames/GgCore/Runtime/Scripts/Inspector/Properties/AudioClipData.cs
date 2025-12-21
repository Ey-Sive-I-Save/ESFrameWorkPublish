using System;
using System.IO;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class AudioClipData
    {
        #region Variables

        [SerializeField, ReadOnly]
        [Tooltip("The AudioClip asset reference for the AudioClipData")]
        private AudioClip audioClip;
        
        [SerializeField, ReadOnly]
        [Tooltip("The asset name of the audio clip.")]
        private string fileName;

        [SerializeField, ReadOnly]
        [Tooltip("The asset path of the audio clip.")]
        private string assetPath;

        [SerializeField, ReadOnly, Space]
        [Tooltip("Force AudioClips to mono?")]
        private bool forceToMono; 
        
        /*
        [SerializeField, ReadOnly, Indent]
        [Tooltip("")]
        private bool normalise;
        */

        [SerializeField, ReadOnly]
        [Tooltip("Corresponding to the \"Load In Background\" flag in the AudioClip inspector, when this flag is set, the loading of the clip will happen delayed without blocking the main thread.")]
        private bool loadInBackground;

        [SerializeField, ReadOnly]
        [Tooltip("When this flag is set, the audio clip will be treated as being ambisonic.")]
        private bool ambisonic;

        [SerializeField, ReadOnly, Space]
        [Tooltip("LoadType defines how the imported AudioClip data should be loaded.")]
        private AudioClipLoadType loadType;

        [SerializeField, ReadOnly]
        [Tooltip("Preloads audio data of the clip when the clip asset is loaded. When this flag is off, scripts have to call AudioClip.LoadAudioData() to load the data before the clip can be played. Properties like length, channels and format are available before the audio data has been loaded.")]
        private bool preloadAudioData;

        [SerializeField, ReadOnly]
        [Tooltip("CompressionFormat defines the compression type that the audio file is encoded to. Different compression types have different performance and audio artifact characteristics.")]
        private AudioCompressionFormat compressionFormat;

        [SerializeField, ReadOnly, Range(1, 100)]
        [Tooltip("Audio compression quality (0-1) Amount of compression. The value roughly corresponds to the ratio between the resulting and the source file sizes.")]
        private float quality;

#if UNITY_EDITOR
        [field:SerializeField, ReadOnly]
        [Tooltip("Defines how the sample rate is modified (if at all) of the importer audio file.")]
        private UnityEditor.AudioSampleRateSetting sampleRateSetting;
        
        public UnityEditor.AudioSampleRateSetting Editor_SampleRateSetting
        {
            get => sampleRateSetting;
            set => sampleRateSetting = value;
        }
#endif

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Constructors

        /// <summary>
        /// AudioClipData caches the values of an audioClip's fileName, assetPath and import settings.
        /// New AudioClipData can be created at runtime, but some variables may not be correctly set if created from null or audioClip types.
        /// </summary>
        /// <param name="audioClipData"></param>
        public AudioClipData(AudioClipData audioClipData)
        {
            this.audioClip = audioClipData.audioClip;
            this.fileName = audioClipData.fileName;
            this.assetPath = audioClipData.assetPath;
            
            this.forceToMono = audioClipData.forceToMono;
            this.loadInBackground = audioClipData.loadInBackground;
            this.ambisonic = audioClipData.ambisonic;
            
            this.loadType = audioClipData.loadType;
            this.preloadAudioData = audioClipData.preloadAudioData;
            this.compressionFormat = audioClipData.compressionFormat;
            this.quality = audioClipData.quality;
            
#if UNITY_EDITOR
            this.sampleRateSetting = audioClipData.sampleRateSetting;
#endif
        }

        /// <summary>
        /// AudioClipData caches the values of an audioClip's fileName, assetPath and import settings.
        /// New AudioClipData can be created at runtime, but some variables may not be correctly set if created from null or audioClip types.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="assetPath"></param>
        /// <param name="forceToMono"></param>
        /// <param name="loadInBackground"></param>
        /// <param name="ambisonic"></param>
        /// <param name="loadType"></param>
        /// <param name="preloadAudioData"></param>
        /// <param name="compressionFormat"></param>
        /// <param name="quality"></param>
        public AudioClipData(string fileName, string assetPath, bool forceToMono, bool loadInBackground, bool ambisonic, AudioClipLoadType loadType, bool preloadAudioData, AudioCompressionFormat compressionFormat, float quality)
        {
#if UNITY_EDITOR
            this.audioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#endif
            
            this.fileName = fileName;
            this.assetPath = assetPath;
            
            this.forceToMono = forceToMono;
            this.loadInBackground = loadInBackground;
            this.ambisonic = ambisonic;
            
            this.loadType = loadType;
            this.preloadAudioData = preloadAudioData;
            this.compressionFormat = compressionFormat;
            this.quality = quality;
            
#if UNITY_EDITOR
            //this.sampleRateSetting = audioClip.sampleRateSetting;
#endif
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region EditorOnly static funtions
        
#if UNITY_EDITOR
        internal static bool Editor_CreateAudioClipData(AudioClip clip, out AudioClipData audioClipData)
        {
            string filePath = UnityEditor.AssetDatabase.GetAssetOrScenePath(clip);
            if (Editor_CreateAudioClipData(filePath, out AudioClipData clipData))
            {
                audioClipData = clipData;
                return true;
            }
            audioClipData = null;
            return false;
        }
        
        public static bool Editor_CreateAudioClipData(string filePath, out AudioClipData audioClipData)
        {
            if (!IsCompatibleType(filePath) || IsStreamed(filePath))
            {
                audioClipData = null;
                return false;
            }
            
            UnityEditor.AudioImporter importer = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(filePath);

            // get constructor values
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string assetPath = filePath;
            bool forceToMono = importer.forceToMono;
            bool loadInBackground = importer.loadInBackground;
            bool ambisonic = importer.ambisonic;
            AudioClipLoadType loadType = importer.defaultSampleSettings.loadType;
#if UNITY_2022_2_OR_NEWER
            bool preloadAudioData = importer.GetOverrideSampleSettings(GetRuntimePlatformString()).preloadAudioData;
#else
            bool preloadAudioData = importer.preloadAudioData;
#endif
            AudioCompressionFormat compressionFormat = importer.defaultSampleSettings.compressionFormat;
            float quality = importer.defaultSampleSettings.quality;
            UnityEditor.AudioSampleRateSetting sampleRateSetting = importer.defaultSampleSettings.sampleRateSetting;
            
            // return newly created AudioClipData
            audioClipData = new AudioClipData(fileName, assetPath, forceToMono, loadInBackground, ambisonic, loadType, preloadAudioData, compressionFormat, quality) 
            {
                Editor_SampleRateSetting = sampleRateSetting
            };
            return true;
        }
#endif

        private static bool IsCompatibleType(string filePath)
        {
            return filePath.EndsWith(".aif", StringComparison.InvariantCultureIgnoreCase)
                   || filePath.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase)
                   || filePath.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase)
                   || filePath.EndsWith(".ogg", StringComparison.InvariantCultureIgnoreCase)

                   // NOTE: tracker module assets behave like any other audio asset in Unity, although no waveform preview is available
                   || filePath.EndsWith(".xm", StringComparison.InvariantCultureIgnoreCase)
                   || filePath.EndsWith(".mod", StringComparison.InvariantCultureIgnoreCase)
                   || filePath.EndsWith(".it", StringComparison.InvariantCultureIgnoreCase)
                   || filePath.EndsWith(".s3m", StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool IsStreamed(string filePath)
        {
            string localStreamingAssetsPath = Application.streamingAssetsPath;
            int indexOfAssets = Application.streamingAssetsPath.IndexOf("/Assets/", StringComparison.Ordinal);
            if (0 < indexOfAssets)
            {
                localStreamingAssetsPath = Application.streamingAssetsPath.Substring(indexOfAssets + 1);
            }
            
            return filePath.Contains(localStreamingAssetsPath);
        }

#if UNITY_2022_2_OR_NEWER

    private static string GetRuntimePlatformString()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
                return "iOS";
            case RuntimePlatform.WebGLPlayer:
                return "WebPlayer";
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxServer:
            case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.EmbeddedLinuxArm32:
            case RuntimePlatform.EmbeddedLinuxArm64:
            case RuntimePlatform.EmbeddedLinuxX64:
            case RuntimePlatform.EmbeddedLinuxX86:
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsServer:
                return "Standalone";
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.PS4:
                return "PS4";
            case RuntimePlatform.XboxOne:
                return "XBoxOne";
            default:
                return string.Empty;
        }
    }

#endif
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Getter

        /// <summary>
        /// The AudioClip asset reference for the AudioClipData
        /// </summary>
        public AudioClip Clip => audioClip;

        /// <summary>
        /// The asset name of the audio clip.
        /// </summary>
        public string FileName => fileName;

        /// <summary>
        /// The asset path of the audio clip.
        /// </summary>
        public string AssetPath => assetPath;

        /// <summary>
        /// Force AudioClips to mono?
        /// </summary>
        public bool ForceToMono => forceToMono;

        /// <summary>
        /// Corresponding to the "Load In Background" flag in the AudioClip inspector, when this flag is set, the loading of the clip will happen delayed without blocking the main thread.
        /// </summary>
        public bool LoadInBackground => loadInBackground;

        /// <summary>
        /// When this flag is set, the audio clip will be treated as being ambisonic.
        /// </summary>
        public bool Ambisonic => ambisonic;

        /// <summary>
        /// LoadType defines how the imported AudioClip data should be loaded.
        /// </summary>
        public AudioClipLoadType LoadType => loadType;

        /// <summary>
        /// Preloads audio data of the clip when the clip asset is loaded. When this flag is off, scripts have to call AudioClip.LoadAudioData() to load the data before the clip can be played. Properties like length, channels and format are available before the audio data has been loaded.
        /// </summary>
        public bool PreloadAudioData => preloadAudioData;

        /// <summary>
        /// CompressionFormat defines the compression type that the audio file is encoded to. Different compression types have different performance and audio artifact characteristics.
        /// </summary>
        public AudioCompressionFormat CompressionFormat => compressionFormat;

        /// <summary>
        /// Audio compression quality (0-1) Amount of compression. The value roughly corresponds to the ratio between the resulting and the source file sizes.
        /// </summary>
        public float Quality => quality;

        #endregion
        
    } // class end
}