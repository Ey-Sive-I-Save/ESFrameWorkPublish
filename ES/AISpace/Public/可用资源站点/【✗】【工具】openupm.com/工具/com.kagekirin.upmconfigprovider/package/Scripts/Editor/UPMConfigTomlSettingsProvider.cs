using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Tomlyn;
using Tomlyn.Model;

#nullable enable


namespace KageKirin.UPMConfig
{
    public class UPMConfigSettingsProvider : SettingsProvider
    {
        private static string k_UPMConfigTomlPath =>
            Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".upmconfig.toml"
            );

        private const string k_UPMConfigTomlDefault =
            @"[npmAuth.""https://new.registry""]
token = ""auth token""
email = ""some@email.com""
alwaysAuth = false
";
        private string m_UpmConfigToml = String.Empty;
        private TomlTable? m_tomlData = null;
        private TomlTable? m_npmAuthData = null;
        private List<bool>? m_npmAuthRegistriesFoldout = null;
        private bool m_listFoldout = false;

        class Styles
        {
            /// formatting
            public static GUIContent upmConfigToml = new GUIContent(".upmconfig.toml");
            public static GUIContent upmConfigTomlInfo = new GUIContent(
                @"A `.upmconfig.toml` file must reside in your $HOME directory.
This seems not to be the case at the moment.
Please press the button below to create it."
            );
            public static GUIContent upmConfigTomlButton = new GUIContent("Create .upmconfig.toml");

            public static GUILayoutOption[] upmConfigTomlOptions = new GUILayoutOption[]
            {
                GUILayout.Height(400),
                GUILayout.Width(EditorGUIUtility.labelWidth),
                GUILayout.MaxWidth(1200),
            };

            public static GUIStyle upmConfigTomlInfoSpaceStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperLeft,
            };

            public static GUILayoutOption[] upmConfigTomlInfoSpaceOptions = new GUILayoutOption[]
            {
                GUILayout.Width(EditorGUIUtility.labelWidth),
                GUILayout.MaxWidth(1200),
            };
            public static GUILayoutOption[] upmConfigTomlButtonSpaceOptions = new GUILayoutOption[]
            {
                GUILayout.Width(EditorGUIUtility.labelWidth),
                GUILayout.MaxWidth(1200),
            };
        }

        private void InitializeData()
        {
            if (File.Exists(k_UPMConfigTomlPath))
            {
                m_tomlData = UPMConfigSerializer.Read(k_UPMConfigTomlPath);
                m_npmAuthData = (TomlTable)m_tomlData["npmAuth"];

                m_npmAuthRegistriesFoldout = new List<bool>(m_npmAuthData.Count);

                Debug.Log(m_npmAuthData.ToString());
                foreach (var reg in m_npmAuthData)
                {
                    Debug.Log(reg.ToString());
                }
            }
        }

        private void FinalizeData()
        {
            if (m_tomlData != null)
            {
                UPMConfigSerializer.Write(k_UPMConfigTomlPath, m_tomlData);
            }
        }

        private void CreateUpmConfigToml()
        {
            File.WriteAllText(k_UPMConfigTomlPath, k_UPMConfigTomlDefault);
            InitializeData();
        }

        public UPMConfigSettingsProvider(
            string path,
            SettingsScope scopes,
            IEnumerable<string> keywords = null
        ) : base(path, scopes, keywords) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            InitializeData();
        }

        public override void OnDeactivate()
        {
            FinalizeData();
        }

        public override void OnGUI(string searchContext)
        {
            using (CreateSettingsWindowGUIScope())
            {
                if (m_tomlData == null && m_npmAuthData == null)
                {
                    GUILayout.Box(
                        Styles.upmConfigTomlInfo,
                        Styles.upmConfigTomlInfoSpaceStyle,
                        Styles.upmConfigTomlInfoSpaceOptions
                    );
                    if (
                        GUILayout.Button(
                            Styles.upmConfigTomlButton,
                            Styles.upmConfigTomlButtonSpaceOptions
                        )
                    )
                    {
                        CreateUpmConfigToml();
                    }
                    return;
                }
                m_listFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                    m_listFoldout,
                    $"Registries ({m_npmAuthData.Count})"
                );
                if (m_listFoldout)
                {
                    int newCount = EditorGUILayout.DelayedIntField("Size", m_npmAuthData.Count);
                    int minCount = (int)MathF.Min(newCount, m_npmAuthData.Count);
                    while (newCount > m_npmAuthRegistriesFoldout.Count)
                    {
                        m_npmAuthRegistriesFoldout.Add(false);
                    }

                    for (int i = 0; i < minCount; i++)
                    {
                        var registryName = m_npmAuthData.Keys.ElementAt(i);
                        var registryData = (TomlTable)m_npmAuthData[registryName];

                        EditorGUI.indentLevel++;

                        m_npmAuthRegistriesFoldout[i] = EditorGUILayout.Foldout(
                            m_npmAuthRegistriesFoldout[i],
                            registryName
                        );
                        if (m_npmAuthRegistriesFoldout[i])
                        {
                            EditorGUI.indentLevel++;

                            var newRegistryName = EditorGUILayout.TextField(
                                "registry",
                                (string)registryName
                            );
                            registryData["token"] = EditorGUILayout.TextField(
                                "token",
                                (string)registryData["token"]
                            );
                            registryData["email"] = EditorGUILayout.TextField(
                                "email",
                                (string)registryData["email"]
                            );
                            registryData["alwaysAuth"] = EditorGUILayout.Toggle(
                                "alwaysAuth",
                                (bool)registryData["alwaysAuth"]
                            );

                            if (newRegistryName != registryName)
                            {
                                m_npmAuthData.Add(
                                    newRegistryName,
                                    Toml.ToModel(Toml.FromModel(registryData))
                                );
                                m_npmAuthData.Remove(registryName);
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("+"))
                        newCount++;
                    if (GUILayout.Button("-"))
                        newCount--;
                    EditorGUILayout.EndHorizontal();

                    // remove last element(s)
                    while (newCount < m_npmAuthData.Count)
                    {
                        var lastElement = m_npmAuthData.Keys.ElementAt(m_npmAuthData.Count - 1);
                        m_npmAuthData.Remove(lastElement);
                    }

                    // add new element(s)
                    while (newCount > m_npmAuthData.Count)
                    {
                        var newElement = new TomlTable();
                        newElement.Add("token", "new token");
                        newElement.Add("email", "new@email.com");
                        newElement.Add("alwaysAuth", false);
                        m_npmAuthData.Add($"https://new.registry_{newCount}", newElement);
                        m_npmAuthRegistriesFoldout[m_npmAuthData.Count - 1] = true;
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save"))
                {
                    FinalizeData();
                }
                if (GUILayout.Button("Load"))
                {
                    InitializeData();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateUPMConfigSettingsProvider()
        {
            var provider = new UPMConfigSettingsProvider(
                "Project/.upmconfig.toml",
                SettingsScope.Project,
                GetSearchKeywordsFromGUIContentProperties<Styles>()
            );
            return provider;
        }

        private IDisposable CreateSettingsWindowGUIScope()
        {
            var unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));
            var type = unityEditorAssembly.GetType("UnityEditor.SettingsWindow+GUIScope");
            return Activator.CreateInstance(type) as IDisposable;
        }
    }
} // namespace KageKirin.UPMConfig
