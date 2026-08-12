using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Consumer 引用的显式编辑入口。所有源资产写入都由用户操作触发，
    /// 与只分析并输出 Catalog/ReferenceGraph 的 ESAssetReferenceBaker 分离。
    /// </summary>
    internal static class ESAssetConsumerReferenceAuthoring
    {
        internal static bool TryAddManualGameCoreAsset(ESAssetLibraryConsumer consumer, UnityEngine.Object asset)
        {
            if (consumer == null || !(asset is ScriptableObject scriptableObject)
                || ESScriptableObjectClassification.GetClass(scriptableObject) != ESScriptableObjectClass.GameCore)
                return false;
            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            if (!identity.IsValid) return false;
            consumer.ManualGameCoreAssets ??= new List<ESAssetReferBase>();
            if (consumer.ManualGameCoreAssets.Any(item => item != null && item.AssetIdentity.Equals(new ESAssetIdentity(identity.guid, identity.localFileId))))
                return true;
            var refer = new ESAssetReferScriptableObject();
            refer.InitializeGeneratedReference(identity.guid, identity.localFileId, ESAssetReferKind.ScriptableObject, 0, string.Empty);
            Undo.RecordObject(consumer, "Add Manual GameCore Asset");
            consumer.ManualGameCoreAssets.Add(refer);
            EditorUtility.SetDirty(consumer);
            return true;
        }

        internal static bool TryAddResidentAsset(ESAssetLibraryConsumer consumer, UnityEngine.Object asset, out string error)
        {
            error = string.Empty;
            if (consumer == null || asset == null)
            {
                error = "Consumer 或资产为空。";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path) || ESAssetPipelineIO.IsEditorOnly(path, asset))
            {
                error = "脚本、EditorOnly 或无效资产不能作为启动常驻资产。";
                return false;
            }
            if (asset is SceneAsset)
            {
                error = "Scene 不能作为常驻对象加载，请使用场景加载流程。";
                return false;
            }
            if (asset is ScriptableObject scriptableObject
                && ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.GameCore)
            {
                error = "IGameCoreSO 应放入 GameCoreAssets，不能重复放入 ResidentAssets。";
                return false;
            }

            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            if (!identity.IsValid)
            {
                error = "资产缺少有效 GUID/LocalFileId。";
                return false;
            }

            ESAssetPage page = null;
            foreach (ESAssetReferKind kind in Enum.GetValues(typeof(ESAssetReferKind)))
                if (kind != ESAssetReferKind.None
                    && ESAssetRegistry.TryGetByAssetIdentity(kind, identity.guid, identity.localFileId, out page))
                    break;
            if (page == null)
            {
                error = "资产尚未注册到 AssetLibrary，请先完成资源注册。";
                return false;
            }

            consumer.ResidentAssets ??= new List<ESAssetReferBase>();
            if (consumer.ResidentAssets.Any(item => item != null && item.AssetIdentity.Equals(new ESAssetIdentity(identity.guid, identity.localFileId))))
                return true;

            var refer = new ESAssetReferUnityObject();
            refer.InitializeGeneratedReference(identity.guid, identity.localFileId, page.Kind, page.EnumKey, page.EffectiveStringKey);
            Undo.RecordObject(consumer, "Add Resident Asset");
            consumer.ResidentAssets.Add(refer);
            EditorUtility.SetDirty(consumer);
            return true;
        }
    }
}
