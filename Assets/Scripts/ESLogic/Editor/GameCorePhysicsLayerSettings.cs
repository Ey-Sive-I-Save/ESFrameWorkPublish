using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 将 GameCoreEditorGlobalData 的物理层声明同步到 Unity 的 TagManager 与 Physics 碰撞矩阵。
    /// 该工具只改动已在规则中声明的 Layer；其他 Layer 的名字和碰撞关系保持原样。
    /// </summary>
    public static class GameCorePhysicsLayerSettings
    {
        private const string DataPath = "Assets/ESNormalAssets/Data/GlobalData/GameCore/GameCoreEditorGlobalData.asset";
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        [MenuItem("【ES】/项目设置/GameCore/Physics Layer/同步 Unity Layer 与碰撞矩阵", priority = 30)]
        public static void SynchronizeProjectSettings()
        {
            if (!TryGetRules(out GameCoreEditorGlobalData data, out Dictionary<string, GameCorePhysicsLayerRule> rulesByName, out List<string> errors))
            {
                LogErrors("无法同步 GameCore Physics Layer 规则", errors);
                return;
            }

            SynchronizeLayerNames(data.physicsLayers);
            SynchronizeCollisionMatrix(data.physicsLayers, rulesByName);
            AssetDatabase.SaveAssets();

            List<string> validationErrors = ValidateProjectSettings(data.physicsLayers, rulesByName);
            if (validationErrors.Count > 0)
            {
                LogErrors("GameCore Physics Layer 同步后校验失败", validationErrors);
                return;
            }

            Debug.Log($"[GameCorePhysicsLayer] 已同步 {data.physicsLayers.Count} 条 Layer 规则和对应碰撞矩阵。", data);
        }

        [MenuItem("【ES】/项目设置/GameCore/Physics Layer/验证 Unity Layer 与碰撞矩阵", priority = 31)]
        public static void ValidateProjectSettingsMenu()
        {
            if (!TryGetRules(out GameCoreEditorGlobalData data, out Dictionary<string, GameCorePhysicsLayerRule> rulesByName, out List<string> errors))
            {
                LogErrors("GameCore Physics Layer 规则无效", errors);
                return;
            }

            errors = ValidateProjectSettings(data.physicsLayers, rulesByName);
            if (errors.Count == 0)
                Debug.Log($"[GameCorePhysicsLayer] Unity Layer 和碰撞矩阵已符合 {data.physicsLayers.Count} 条规则。", data);
            else
                LogErrors("GameCore Physics Layer 项目设置不符合规则", errors);
        }

        [MenuItem("【ES】/项目设置/GameCore/Physics Layer/验证已加载场景 Collider Trigger 规则", priority = 32)]
        public static void ValidateLoadedSceneColliderTriggers()
        {
            if (!TryGetRules(out GameCoreEditorGlobalData data, out Dictionary<string, GameCorePhysicsLayerRule> rulesByName, out List<string> errors))
            {
                LogErrors("GameCore Physics Layer 规则无效", errors);
                return;
            }

            Dictionary<int, GameCorePhysicsLayerRule> rulesByLayer = new Dictionary<int, GameCorePhysicsLayerRule>();
            foreach (GameCorePhysicsLayerRule rule in rulesByName.Values)
                rulesByLayer.Add(rule.unityLayer, rule);

            Collider[] colliders = Resources.FindObjectsOfTypeAll<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || EditorUtility.IsPersistent(collider) || !collider.gameObject.scene.IsValid() || !collider.gameObject.scene.isLoaded)
                    continue;

                if (!rulesByLayer.TryGetValue(collider.gameObject.layer, out GameCorePhysicsLayerRule rule))
                    continue;

                if (collider.isTrigger != rule.mustBeTrigger)
                {
                    errors.Add($"{GetHierarchyPath(collider.transform)} (Layer {rule.unityLayer}: {rule.semanticName}) 的 isTrigger={collider.isTrigger}，规则要求 {rule.mustBeTrigger}。");
                }
            }

            if (errors.Count == 0)
                Debug.Log("[GameCorePhysicsLayer] 已加载场景中的 Collider Trigger 规则全部通过。", data);
            else
                LogErrors("已加载场景 Collider Trigger 规则不符合", errors);
        }

        private static bool TryGetRules(
            out GameCoreEditorGlobalData data,
            out Dictionary<string, GameCorePhysicsLayerRule> rulesByName,
            out List<string> errors)
        {
            data = AssetDatabase.LoadAssetAtPath<GameCoreEditorGlobalData>(DataPath);
            rulesByName = new Dictionary<string, GameCorePhysicsLayerRule>();
            errors = new List<string>();

            if (data == null)
            {
                errors.Add($"找不到 {DataPath}。请先使用“打开或创建GameCore编辑器全局数据”创建并初始化资产。");
                return false;
            }

            if (data.physicsLayers == null || data.physicsLayers.Count == 0)
            {
                errors.Add("physicsLayers 为空。请在 GameCoreEditorGlobalData 中执行“初始化推荐配置”。");
                return false;
            }

            HashSet<int> layerNumbers = new HashSet<int>();
            for (int i = 0; i < data.physicsLayers.Count; i++)
            {
                GameCorePhysicsLayerRule rule = data.physicsLayers[i];
                if (rule == null)
                {
                    errors.Add($"physicsLayers[{i}] 为空。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rule.semanticName))
                {
                    errors.Add($"physicsLayers[{i}] 缺少语义名。");
                    continue;
                }

                if (rule.unityLayer < 0 || rule.unityLayer > 31)
                    errors.Add($"{rule.semanticName} 的 Layer 编号 {rule.unityLayer} 超出 0-31。");

                if (!layerNumbers.Add(rule.unityLayer))
                    errors.Add($"Layer 编号 {rule.unityLayer} 被重复声明。");

                if (rulesByName.ContainsKey(rule.semanticName))
                    errors.Add($"Layer 语义名 {rule.semanticName} 被重复声明。");
                else
                    rulesByName.Add(rule.semanticName, rule);
            }

            foreach (GameCorePhysicsLayerRule rule in rulesByName.Values)
            {
                if (rule.forbiddenCollisionLayers == null)
                    continue;

                for (int i = 0; i < rule.forbiddenCollisionLayers.Count; i++)
                {
                    string target = rule.forbiddenCollisionLayers[i];
                    if (string.IsNullOrWhiteSpace(target) || !rulesByName.ContainsKey(target))
                        errors.Add($"{rule.semanticName} 的禁止碰撞目标 {target ?? "<null>"} 未在 physicsLayers 中声明。");
                }
            }

            return errors.Count == 0;
        }

        private static void SynchronizeLayerNames(IReadOnlyList<GameCorePhysicsLayerRule> rules)
        {
            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogError($"[GameCorePhysicsLayer] 无法加载 {TagManagerPath}。");
                return;
            }

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray || layers.arraySize != 32)
            {
                Debug.LogError("[GameCorePhysicsLayer] TagManager.layers 不是 32 项 Layer 数组。");
                return;
            }

            bool changed = false;
            for (int i = 0; i < rules.Count; i++)
            {
                GameCorePhysicsLayerRule rule = rules[i];
                SerializedProperty layerName = layers.GetArrayElementAtIndex(rule.unityLayer);
                string expectedName = GetUnityLayerName(rule);
                if (layerName.stringValue == expectedName)
                    continue;

                layerName.stringValue = expectedName;
                changed = true;
            }

            if (changed)
                tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SynchronizeCollisionMatrix(
            IReadOnlyList<GameCorePhysicsLayerRule> rules,
            IReadOnlyDictionary<string, GameCorePhysicsLayerRule> rulesByName)
        {
            for (int leftIndex = 0; leftIndex < rules.Count; leftIndex++)
            {
                GameCorePhysicsLayerRule left = rules[leftIndex];
                for (int rightIndex = leftIndex; rightIndex < rules.Count; rightIndex++)
                {
                    GameCorePhysicsLayerRule right = rules[rightIndex];
                    bool ignored = ForbidsCollision(left, right, rulesByName)
                                   || ForbidsCollision(right, left, rulesByName);
                    Physics.IgnoreLayerCollision(left.unityLayer, right.unityLayer, ignored);
                }
            }
        }

        private static List<string> ValidateProjectSettings(
            IReadOnlyList<GameCorePhysicsLayerRule> rules,
            IReadOnlyDictionary<string, GameCorePhysicsLayerRule> rulesByName)
        {
            List<string> errors = new List<string>();
            for (int leftIndex = 0; leftIndex < rules.Count; leftIndex++)
            {
                GameCorePhysicsLayerRule left = rules[leftIndex];
                string expectedName = GetUnityLayerName(left);
                string actualName = LayerMask.LayerToName(left.unityLayer);
                if (actualName != expectedName)
                    errors.Add($"Layer {left.unityLayer} 期望名称 {expectedName}，实际为 {actualName ?? "<空>"}。");

                for (int rightIndex = leftIndex; rightIndex < rules.Count; rightIndex++)
                {
                    GameCorePhysicsLayerRule right = rules[rightIndex];
                    bool expectedIgnored = ForbidsCollision(left, right, rulesByName)
                                           || ForbidsCollision(right, left, rulesByName);
                    bool actualIgnored = Physics.GetIgnoreLayerCollision(left.unityLayer, right.unityLayer);
                    if (actualIgnored != expectedIgnored)
                    {
                        errors.Add($"碰撞矩阵 {left.semanticName}({left.unityLayer}) <-> {right.semanticName}({right.unityLayer}) 应为 "
                                   + (expectedIgnored ? "禁止" : "允许") + "，实际为 " + (actualIgnored ? "禁止" : "允许") + "。");
                    }
                }
            }

            return errors;
        }

        private static bool ForbidsCollision(
            GameCorePhysicsLayerRule source,
            GameCorePhysicsLayerRule target,
            IReadOnlyDictionary<string, GameCorePhysicsLayerRule> rulesByName)
        {
            if (source.forbiddenCollisionLayers == null)
                return false;

            for (int i = 0; i < source.forbiddenCollisionLayers.Count; i++)
            {
                string semanticName = source.forbiddenCollisionLayers[i];
                if (semanticName == target.semanticName)
                    return true;

                if (rulesByName.TryGetValue(semanticName, out GameCorePhysicsLayerRule declaredTarget)
                    && declaredTarget.unityLayer == target.unityLayer)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetUnityLayerName(GameCorePhysicsLayerRule rule)
        {
            return rule.semanticName == "IgnoreRaycast" ? "Ignore Raycast" : rule.semanticName;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            StringBuilder builder = new StringBuilder(transform.name);
            Transform current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static void LogErrors(string title, IReadOnlyList<string> errors)
        {
            Debug.LogError($"[GameCorePhysicsLayer] {title}\n- {string.Join("\n- ", errors)}");
        }
    }
}
