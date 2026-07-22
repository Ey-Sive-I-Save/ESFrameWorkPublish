using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    /// <summary>
    /// 泛型预览编辑器基类。可实现任意 UnityEngine.Object 的模块化预览。
    /// </summary>
    /// <typeparam name="T">需要显示预览的目标类型，必须继承自 UnityEngine.Object。</typeparam>
    public abstract class BasePreviewEditor<T> : OdinEditor where T : UnityEngine.Object
    {
        private const string FoldoutNormalPrefsKey = "ES_EditorPreview_FoldoutNormal";
        private const string FoldoutSinglePrefsKey = "ES_EditorPreview_FoldoutSingle";
        private const double PreviewEditorUpdateInterval = 1d / 30d;
        private const float PreviewEditorMaxDeltaTime = 1f / 15f;

        private Vector2 scrollPosition;

        private readonly List<IPreviewElement> normalProviders = new List<IPreviewElement>();
        private readonly List<IPreviewElement> singleProviders = new List<IPreviewElement>();
        private readonly HashSet<IPreviewElement> collectedProviders = new HashSet<IPreviewElement>();
        private readonly HashSet<IPreviewElement> activeProviders = new HashSet<IPreviewElement>();
        private readonly List<IPreviewElement> inactiveBuffer = new List<IPreviewElement>();

        // Collector/子类扩展先写入缓冲区，再统一经过 AddPreviewElement 做过滤、去重和分区。
        // Override 和 Collector 分开使用缓冲区，避免子类调用 base.CollectPreviewElements 时互相清空。
        private readonly List<IPreviewElement> overrideNormalBuffer = new List<IPreviewElement>();
        private readonly List<IPreviewElement> overrideSingleBuffer = new List<IPreviewElement>();
        private readonly List<IPreviewElement> collectorNormalBuffer = new List<IPreviewElement>();
        private readonly List<IPreviewElement> collectorSingleBuffer = new List<IPreviewElement>();
        private double lastEditorUpdateTime;

        // 这里保持 static：所有预览编辑器共享折叠状态，这是当前工具的设计。
        private static bool foldoutNormal;
        private static bool foldoutSingle;

        protected override void OnEnable()
        {
            base.OnEnable();

            foldoutNormal = EditorPrefs.GetBool(FoldoutNormalPrefsKey, true);
            foldoutSingle = EditorPrefs.GetBool(FoldoutSinglePrefsKey, true);
            lastEditorUpdateTime = EditorApplication.timeSinceStartup;

            EditorApplication.update -= OnPreviewEditorUpdate;
            EditorApplication.update += OnPreviewEditorUpdate;
        }

        protected override void OnDisable()
        {
            EditorApplication.update -= OnPreviewEditorUpdate;
            ReleaseAllActivePreviewElements(dispose: true);
            base.OnDisable();
        }

        public override bool HasPreviewGUI() => true;

        /// <summary>
        /// 收集目标对象上的预览模块。
        /// 子类可以继续 override 这个方法并向传入列表添加元素，最终会由基类统一过滤和去重。
        /// </summary>
        protected virtual void CollectPreviewElements(
            T targetObject,
            List<IPreviewElement> normalProviders,
            List<IPreviewElement> singleProviders)
        {
            if (targetObject is IPreviewElement element)
                AddPreviewElement(element);

            if (targetObject is IPreviewCollector collector)
                CollectFromCollector(collector);
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var targetObj = target as T;
            if (targetObj == null)
            {
                ReleaseAllActivePreviewElements(dispose: true);
                EditorGUI.LabelField(r, $"未找到 {typeof(T).Name} 实例");
                return;
            }

            normalProviders.Clear();
            singleProviders.Clear();
            collectedProviders.Clear();
            CollectPreviewElementsFromTarget(targetObj);
            SyncActivePreviewElements();

            GUILayout.BeginArea(r);
            using (var scrollView = new GUILayout.ScrollViewScope(scrollPosition, false, true))
            {
                scrollPosition = scrollView.scrollPosition;
                GUILayout.BeginVertical();

                DrawHeader(targetObj);
                DrawNormalPreviewArea();
                DrawSinglePreviewArea();

                GUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void DrawHeader(T targetObj)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"{targetObj.name} 预览", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            var stateColor = Application.isPlaying
                ? new Color(0.3f, 1f, 0.3f)
                : new Color(0.7f, 0.7f, 0.7f);

            var style = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = stateColor },
                fontStyle = FontStyle.Bold
            };

            GUILayout.Label(Application.isPlaying ? "● PLAY" : "○ STOP", style);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void DrawNormalPreviewArea()
        {
            if (normalProviders.Count <= 0)
            {
                EditorGUILayout.HelpBox("当前对象没有可用的预览模块。", MessageType.Info);
                return;
            }

            GUILayout.BeginVertical(EditorStyles.helpBox);

            var newFoldout = EditorGUILayout.Foldout(foldoutNormal, "预览模块", true);
            SaveFoldoutIfChanged(FoldoutNormalPrefsKey, ref foldoutNormal, newFoldout);

            if (foldoutNormal)
            {
                GUILayout.Space(4);
                foreach (var provider in normalProviders)
                    DrawProviderContent(provider);
            }

            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private void DrawSinglePreviewArea()
        {
            if (singleProviders.Count <= 0)
                return;

            GUILayout.Space(10);

            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 1f, 0.8f);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalColor;

            var newFoldout = EditorGUILayout.Foldout(foldoutSingle, "大预览区域", true);
            SaveFoldoutIfChanged(FoldoutSinglePrefsKey, ref foldoutSingle, newFoldout);

            if (foldoutSingle)
            {
                foreach (var provider in singleProviders)
                    DrawProviderContent(provider);
            }

            GUILayout.EndVertical();
        }

        private void CollectPreviewElementsFromTarget(T targetObj)
        {
            if (targetObj is GameObject gameObject)
            {
                CollectPreviewElementsFromGameObject(gameObject);
                return;
            }

            if (targetObj is Component component)
            {
                CollectPreviewElementsFromGameObject(component.gameObject);
                return;
            }

            CollectFromOverride(targetObj);
        }

        private void CollectPreviewElementsFromGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null)
                    continue;

                if (component is IPreviewElement element)
                    AddPreviewElement(element);

                if (component is IPreviewCollector collector)
                    CollectFromCollector(collector);
            }
        }

        private void CollectFromOverride(T targetObj)
        {
            overrideNormalBuffer.Clear();
            overrideSingleBuffer.Clear();

            try
            {
                CollectPreviewElements(targetObj, overrideNormalBuffer, overrideSingleBuffer);
                AddPreviewElementsFromBuffer(overrideNormalBuffer);
                AddPreviewElementsFromBuffer(overrideSingleBuffer);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                overrideNormalBuffer.Clear();
                overrideSingleBuffer.Clear();
            }
        }

        private void CollectFromCollector(IPreviewCollector collector)
        {
            if (collector == null)
                return;

            collectorNormalBuffer.Clear();
            collectorSingleBuffer.Clear();

            try
            {
                collector.CollectPreviewElements(collectorNormalBuffer, collectorSingleBuffer);
                AddPreviewElementsFromBuffer(collectorNormalBuffer);
                AddPreviewElementsFromBuffer(collectorSingleBuffer);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                collectorNormalBuffer.Clear();
                collectorSingleBuffer.Clear();
            }
        }

        private void AddPreviewElementsFromBuffer(List<IPreviewElement> providers)
        {
            foreach (var provider in providers)
                AddPreviewElement(provider);
        }

        private void AddPreviewElement(IPreviewElement element)
        {
            if (element == null || !element.CanPreview)
                return;

            if (!collectedProviders.Add(element))
                return;

            if (element.IsSingleArea)
            {
                singleProviders.Add(element);
                return;
            }

            if (element is IPreviewAreaModeProvider modeProvider
                && modeProvider.PreviewAreaMode == PreviewAreaMode.Large)
                singleProviders.Add(element);
            else
                normalProviders.Add(element);
        }

        private void SyncActivePreviewElements()
        {
            inactiveBuffer.Clear();
            foreach (var provider in activeProviders)
            {
                if (provider == null || !collectedProviders.Contains(provider) || !provider.CanPreview)
                    inactiveBuffer.Add(provider);
            }

            for (int i = 0; i < inactiveBuffer.Count; i++)
                DeactivatePreviewElement(inactiveBuffer[i], dispose: true);

            inactiveBuffer.Clear();

            foreach (var provider in collectedProviders)
            {
                if (provider == null || activeProviders.Contains(provider))
                    continue;

                activeProviders.Add(provider);
                ActivatePreviewElement(provider);
            }
        }

        private void ActivatePreviewElement(IPreviewElement provider)
        {
            if (provider is not IPreviewElementLifecycle lifecycle)
                return;

            try
            {
                lifecycle.OnPreviewEnable();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void DeactivatePreviewElement(IPreviewElement provider, bool dispose)
        {
            if (provider != null)
            {
                try
                {
                    if (provider is IPreviewElementLifecycle lifecycle)
                    {
                        lifecycle.OnPreviewDisable();
                        if (dispose)
                            lifecycle.DisposePreview();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            activeProviders.Remove(provider);
        }

        private void ReleaseAllActivePreviewElements(bool dispose)
        {
            inactiveBuffer.Clear();
            inactiveBuffer.AddRange(activeProviders);

            for (int i = 0; i < inactiveBuffer.Count; i++)
                DeactivatePreviewElement(inactiveBuffer[i], dispose);

            inactiveBuffer.Clear();
            activeProviders.Clear();
        }

        private void OnPreviewEditorUpdate()
        {
            if (activeProviders.Count <= 0)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now - lastEditorUpdateTime < PreviewEditorUpdateInterval)
                return;

            float deltaTime = Mathf.Min(PreviewEditorMaxDeltaTime, (float)(now - lastEditorUpdateTime));
            lastEditorUpdateTime = now;

            bool wantsRepaint = false;
            inactiveBuffer.Clear();

            foreach (var provider in activeProviders)
            {
                if (provider == null || !provider.CanPreview)
                {
                    inactiveBuffer.Add(provider);
                    continue;
                }

                if (provider is not IPreviewElementEditorUpdate editorUpdate || !editorUpdate.WantsPreviewEditorUpdate)
                    continue;

                try
                {
                    editorUpdate.OnPreviewEditorUpdate(deltaTime);
                    wantsRepaint = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            for (int i = 0; i < inactiveBuffer.Count; i++)
                DeactivatePreviewElement(inactiveBuffer[i], dispose: true);

            inactiveBuffer.Clear();

            if (wantsRepaint)
                Repaint();
        }

        private static void SaveFoldoutIfChanged(string prefsKey, ref bool currentValue, bool newValue)
        {
            if (currentValue == newValue)
                return;

            currentValue = newValue;
            EditorPrefs.SetBool(prefsKey, newValue);
        }

        private void DrawProviderContent(IPreviewElement provider)
        {
            if (provider == null || !provider.CanPreview)
                return;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(provider.GetType().Name, EditorStyles.boldLabel);

            try
            {
                if (Application.isPlaying)
                    provider.DrawPreviewGUIPlaying();
                else
                    provider.EditorPreviewDrawPreviewGUINonPlay();
            }
            catch (Exception e)
            {
                EditorGUILayout.HelpBox($"预览模块绘制失败：{e.Message}", MessageType.Error);
                Debug.LogException(e);
            }

            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        public override void OnPreviewSettings() { }
    }
}
