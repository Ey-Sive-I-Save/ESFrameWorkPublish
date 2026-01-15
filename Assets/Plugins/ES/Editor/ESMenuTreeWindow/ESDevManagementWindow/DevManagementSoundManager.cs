using UnityEngine;
using UnityEditor;

namespace ES
{
    /// <summary>
    /// 音效管理器 - 为操作提供声音反馈
    /// </summary>
    public static class DevManagementSoundManager
    {
        private static bool soundEnabled = true;
        private const string SoundEnabledKey = "ESDevManagement_SoundEnabled";

        static DevManagementSoundManager()
        {
            soundEnabled = EditorPrefs.GetBool(SoundEnabledKey, true);
        }

        public static bool SoundEnabled
        {
            get => soundEnabled;
            set
            {
                soundEnabled = value;
                EditorPrefs.SetBool(SoundEnabledKey, value);
            }
        }

        /// <summary>
        /// 设置音效开关
        /// </summary>
        public static void SetSoundEnabled(bool enabled)
        {
            SoundEnabled = enabled;
        }

        /// <summary>
        /// 获取音效开关状态
        /// </summary>
        public static bool IsSoundEnabled()
        {
            return soundEnabled;
        }

        /// <summary>
        /// 播放创建成功音效
        /// </summary>
        public static void PlayCreateSound()
        {
            if (!soundEnabled) return;
            // Unity内置音效 - 成功提示音
            EditorApplication.Beep();
        }

        /// <summary>
        /// 播放删除音效
        /// </summary>
        public static void PlayDeleteSound()
        {
            if (!soundEnabled) return;
            EditorApplication.Beep();
        }

        /// <summary>
        /// 播放完成音效
        /// </summary>
        public static void PlayCompleteSound()
        {
            if (!soundEnabled) return;
            EditorApplication.Beep();
        }

        /// <summary>
        /// 播放点击音效
        /// </summary>
        public static void PlayClickSound()
        {
            if (!soundEnabled) return;
            // 点击音效可以更轻一些，这里简化处理
        }

        /// <summary>
        /// 播放警告音效
        /// </summary>
        public static void PlayWarningSound()
        {
            if (!soundEnabled) return;
            EditorApplication.Beep();
        }

        /// <summary>
        /// 播放错误音效
        /// </summary>
        public static void PlayErrorSound()
        {
            if (!soundEnabled) return;
            EditorApplication.Beep();
        }

        /// <summary>
        /// 播放保存音效
        /// </summary>
        public static void PlaySaveSound()
        {
            if (!soundEnabled) return;
            EditorApplication.Beep();
        }
    }
}
