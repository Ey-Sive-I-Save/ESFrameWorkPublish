#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESWorkbenchAssetRegistrationState
    {
        public UnityEngine.Object source;
        public string desiredStringKey = string.Empty;
        public ESContentRegistrationResult lastResult;

        public void InvalidatePreview()
        {
            lastResult = null;
        }
    }

    public readonly struct ESWorkbenchAssetRegistrationSlot
    {
        public readonly string slotId;
        public readonly string label;
        public readonly string help;
        public readonly string targetPropertyPath;
        public readonly string libraryPath;
        public readonly Type objectType;
        public readonly ESAssetReferKind expectedKind;
        public readonly string undoName;
        public readonly string dirtyKey;

        public ESWorkbenchAssetRegistrationSlot(string slotId, string label, string help,
            string targetPropertyPath, string libraryPath, Type objectType,
            ESAssetReferKind expectedKind, string undoName, string dirtyKey)
        {
            this.slotId = slotId;
            this.label = label;
            this.help = help;
            this.targetPropertyPath = targetPropertyPath;
            this.libraryPath = libraryPath;
            this.objectType = objectType;
            this.expectedKind = expectedKind;
            this.undoName = undoName;
            this.dirtyKey = dirtyKey;
        }
    }

    /// <summary>
    /// 工作台普通资产注册的统一流程：显式 StringKey、预检、CAS 提交和 Registry 解析。
    /// 业务工作台只声明槽位，不直接组装 ESContentRegistrationRequest。
    /// </summary>
    public static class ESWorkbenchContentRegistration
    {
        public static ESContentRegistrationResult Preview(UnityEngine.Object source, string desiredKey, string libraryPath)
        {
            if (!TryBuildRequest(source, desiredKey, libraryPath, false, null,
                    out ESContentRegistrationRequest request, out string error))
                return ESContentRegistrationResult.Failure(null, "invalid_request", error);
            return ESContentRegistrationAuthoring.Execute(request);
        }

        public static ESContentRegistrationResult Commit(UnityEngine.Object source, string desiredKey,
            string libraryPath, ESContentRegistrationResult preview)
        {
            if (preview == null || !preview.success || string.IsNullOrEmpty(preview.requestId))
                return ESContentRegistrationResult.Failure(null, "preview_required", "提交前必须完成当前资源与 StringKey 的注册预检。");
            if (!TryBuildRequest(source, desiredKey, libraryPath, true, preview,
                    out ESContentRegistrationRequest request, out string error))
                return ESContentRegistrationResult.Failure(null, "invalid_request", error);
            return ESContentRegistrationAuthoring.Execute(request);
        }

        public static bool TryResolveRegisteredAsset(UnityEngine.Object source, ESAssetReferKind expectedKind,
            out ESAssetPage page, out string error)
        {
            page = null;
            error = string.Empty;
            if (source == null) { error = "资源为空。"; return false; }
            string path = AssetDatabase.GetAssetPath(source);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) { error = "资源没有项目内 GUID，不能作为稳定注册源。"; return false; }
            if (!ESAssetRegistry.TryGetByGuid(expectedKind, guid, out page) || page == null)
            {
                error = "资源尚未按要求的类型注册到 ESAssetRegistry。";
                return false;
            }
            if (string.IsNullOrEmpty(page.EffectiveStringKey))
            {
                error = "已注册资源缺少稳定 StringKey。";
                return false;
            }
            return true;
        }

        private static bool TryBuildRequest(UnityEngine.Object source, string desiredKey, string libraryPath,
            bool commit, ESContentRegistrationResult preview, out ESContentRegistrationRequest request, out string error)
        {
            request = null;
            error = string.Empty;
            if (source == null) { error = "资源为空。"; return false; }
            if (string.IsNullOrEmpty(desiredKey))
            {
                error = "必须显式填写稳定 StringKey；工作台不会静默生成、Trim 或改写 Key。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(libraryPath)) { error = "未配置目标 ESAssetLibrary 项目路径。"; return false; }
            string assetPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "资源必须位于项目 Assets/ 路径下。";
                return false;
            }
            request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                requestId = commit ? preview.requestId : string.Empty,
                commit = commit,
                assetPath = assetPath,
                libraryPath = libraryPath,
                assetKind = "auto",
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = desiredKey
            };
            if (commit)
            {
                request.expectedGuid = preview.guid;
                request.expectedLocalFileId = preview.localFileId;
                request.expectedLibraryRevision = preview.targetRevision;
            }
            return true;
        }
    }
}
#endif
