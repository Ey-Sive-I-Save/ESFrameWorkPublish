using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Formal Scene identity is an editor-side boundary shared by workbenches.
    /// A PreviewScene object is never accepted as evidence for a formal selection.
    /// The persisted representation remains deliberately neutral: scene path plus
    /// GlobalObjectId text, so domain assets do not depend on UnityEditor types.
    /// </summary>
    internal static class ESWorkbenchFormalSceneIdentity
    {
        public static bool TryCapture(
            GameObject candidate,
            out string scenePath,
            out string globalObjectId,
            out string reason)
        {
            scenePath = string.Empty;
            globalObjectId = string.Empty;
            reason = string.Empty;
            if (!IsFormalLoadedSceneObject(candidate, out reason)) return false;

            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(candidate);
            if (id.identifierType == 0)
            {
                reason = "当前正式对象没有可保存的 GlobalObjectId。";
                return false;
            }

            scenePath = candidate.scene.path.Replace('\\', '/');
            globalObjectId = id.ToString();
            if (string.IsNullOrWhiteSpace(scenePath) || string.IsNullOrWhiteSpace(globalObjectId))
            {
                scenePath = string.Empty;
                globalObjectId = string.Empty;
                reason = "正式对象的 Scene 路径或 GlobalObjectId 为空。";
                return false;
            }
            return true;
        }

        public static bool TryResolve(
            string scenePath,
            string globalObjectId,
            out GameObject formalObject,
            out string reason)
        {
            formalObject = null;
            reason = "尚未建立正式 Scene 映射。";
            string normalizedPath = NormalizeScenePath(scenePath);
            if (string.IsNullOrWhiteSpace(globalObjectId)) return false;
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                reason = "formalScenePath 缺失，正式 Scene 映射不完整。";
                return false;
            }
            if (!GlobalObjectId.TryParse(globalObjectId, out GlobalObjectId id))
            {
                reason = "GlobalObjectId 格式无效。";
                return false;
            }

            UnityEngine.Object resolved =
                GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            formalObject = resolved as GameObject ?? (resolved as Component)?.gameObject;
            if (!IsFormalLoadedSceneObject(formalObject, out reason))
            {
                formalObject = null;
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "正式对象已删除、未加载或 GlobalObjectId 已漂移。";
                return false;
            }

            string resolvedPath = NormalizeScenePath(formalObject.scene.path);
            if (!string.Equals(resolvedPath, normalizedPath, StringComparison.Ordinal))
            {
                formalObject = null;
                reason = "映射对象所属 Scene 与记录的 formalScenePath 不一致。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static bool IsFormalLoadedSceneObject(GameObject candidate, out string reason)
        {
            reason = string.Empty;
            if (candidate == null)
            {
                reason = "未选择 GameObject。";
                return false;
            }
            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
            {
                reason = "对象不属于当前已加载的 Scene。";
                return false;
            }
            if (EditorSceneManager.IsPreviewScene(candidate.scene))
            {
                reason = "PreviewScene 对象不能作为正式 Scene 身份证据。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(candidate.scene.path))
            {
                reason = "正式 Scene 尚未保存，不能建立稳定路径映射。";
                return false;
            }
            return true;
        }

        private static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Trim().Replace('\\', '/');
        }
    }
}
