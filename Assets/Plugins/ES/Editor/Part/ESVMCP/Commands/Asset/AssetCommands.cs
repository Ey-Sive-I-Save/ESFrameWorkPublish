using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ES.VMCP
{
    /// <summary>
    /// Asset操作类型
    /// </summary>
    public enum CommonAssetOperation
    {
        CreateAsset,        // 创建Asset
        LoadAsset,          // 加载Asset
        SaveAsset,          // 保存Asset
        DeleteAsset,        // 删除Asset
        CopyAsset,          // 复制Asset
        MoveAsset,          // 移动Asset
        RenameAsset,        // 重命名Asset
        GetAssetPath,       // 获取Asset路径
        CreateFolder,       // 创建文件夹
        ImportAsset,        // 导入Asset
        RefreshAssets,      // 刷新资源数据库
        FindAssets          // 查找Assets
    }

    /// <summary>
    /// 统一的Asset操作命令
    /// </summary>
    [ESVMCPCommand("CommonAssetOperation", "统一的Asset操作命令，支持创建、加载、保存、删除、复制Asset等")]
    public class AssetOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonAssetOperation Operation { get; set; }

        [JsonProperty("assetPath")]
        public string AssetPath { get; set; }

        [JsonProperty("targetPath")]
        public string TargetPath { get; set; }

        [JsonProperty("assetName")]
        public string AssetName { get; set; }

        [JsonProperty("assetType")]
        public string AssetType { get; set; }

        [JsonProperty("searchFilter")]
        public string SearchFilter { get; set; }

        [JsonProperty("folderPath")]
        public string FolderPath { get; set; }

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonAssetOperation.CreateAsset:
                        return $"创建Asset: {AssetPath}";
                    case CommonAssetOperation.LoadAsset:
                        return $"加载Asset: {AssetPath}";
                    case CommonAssetOperation.SaveAsset:
                        return $"保存Asset: {AssetPath}";
                    case CommonAssetOperation.DeleteAsset:
                        return $"删除Asset: {AssetPath}";
                    case CommonAssetOperation.CopyAsset:
                        return $"复制Asset: {AssetPath} -> {TargetPath}";
                    case CommonAssetOperation.MoveAsset:
                        return $"移动Asset: {AssetPath} -> {TargetPath}";
                    case CommonAssetOperation.RenameAsset:
                        return $"重命名Asset: {AssetPath} -> {AssetName}";
                    case CommonAssetOperation.FindAssets:
                        return $"查找Assets: {SearchFilter}";
                    default:
                        return $"Asset操作: {Operation}";
                }
            }
        }

        public override ESVMCPValidationResult Validate()
        {
            switch (Operation)
            {
                case CommonAssetOperation.CreateFolder:
                    if (string.IsNullOrEmpty(FolderPath))
                        return ESVMCPValidationResult.Failure("创建文件夹需要指定folderPath");
                    break;
                case CommonAssetOperation.CopyAsset:
                case CommonAssetOperation.MoveAsset:
                    if (string.IsNullOrEmpty(AssetPath) || string.IsNullOrEmpty(TargetPath))
                        return ESVMCPValidationResult.Failure($"{Operation}操作需要指定assetPath和targetPath");
                    break;
                case CommonAssetOperation.RenameAsset:
                    if (string.IsNullOrEmpty(AssetPath) || string.IsNullOrEmpty(AssetName))
                        return ESVMCPValidationResult.Failure("重命名Asset需要指定assetPath和assetName");
                    break;
                case CommonAssetOperation.FindAssets:
                    if (string.IsNullOrEmpty(SearchFilter))
                        return ESVMCPValidationResult.Failure("查找Assets需要指定searchFilter");
                    break;
                case CommonAssetOperation.RefreshAssets:
                    // RefreshAssets不需要参数
                    break;
                default:
                    if (string.IsNullOrEmpty(AssetPath))
                        return ESVMCPValidationResult.Failure("Asset路径不能为空");
                    break;
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                switch (Operation)
                {
                    case CommonAssetOperation.CreateAsset:
                        return ExecuteCreateAsset(context);
                    case CommonAssetOperation.LoadAsset:
                        return ExecuteLoadAsset(context);
                    case CommonAssetOperation.SaveAsset:
                        return ExecuteSaveAsset(context);
                    case CommonAssetOperation.DeleteAsset:
                        return ExecuteDeleteAsset(context);
                    case CommonAssetOperation.CopyAsset:
                        return ExecuteCopyAsset(context);
                    case CommonAssetOperation.MoveAsset:
                        return ExecuteMoveAsset(context);
                    case CommonAssetOperation.RenameAsset:
                        return ExecuteRenameAsset(context);
                    case CommonAssetOperation.GetAssetPath:
                        return ExecuteGetAssetPath(context);
                    case CommonAssetOperation.CreateFolder:
                        return ExecuteCreateFolder(context);
                    case CommonAssetOperation.ImportAsset:
                        return ExecuteImportAsset(context);
                    case CommonAssetOperation.RefreshAssets:
                        return ExecuteRefreshAssets(context);
                    case CommonAssetOperation.FindAssets:
                        return ExecuteFindAssets(context);
                    default:
                        return ESVMCPCommandResult.Failed($"不支持的操作类型: {Operation}");
                }
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"Asset操作失败: {e.Message}", e);
            }
        }

        private ESVMCPCommandResult ExecuteCreateAsset(ESVMCPExecutionContext context)
        {
            // 这个方法需要配合具体的Asset类型使用
            // 示例：创建Material、Prefab等
            return ESVMCPCommandResult.Succeed($"Asset创建功能需要结合具体类型使用");
        }

        private ESVMCPCommandResult ExecuteLoadAsset(ESVMCPExecutionContext context)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(AssetPath);
            if (asset == null)
            {
                return ESVMCPCommandResult.Failed($"未找到Asset: {AssetPath}");
            }

            return ESVMCPCommandResult.Succeed($"成功加载Asset: {asset.name}", new Dictionary<string, object>
            {
                ["name"] = asset.name,
                ["type"] = asset.GetType().Name,
                ["path"] = AssetPath
            });
        }

        private ESVMCPCommandResult ExecuteSaveAsset(ESVMCPExecutionContext context)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(AssetPath);
            if (asset == null)
            {
                return ESVMCPCommandResult.Failed($"未找到Asset: {AssetPath}");
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            
            return ESVMCPCommandResult.Succeed($"成功保存Asset: {AssetPath}");
        }

        private ESVMCPCommandResult ExecuteDeleteAsset(ESVMCPExecutionContext context)
        {
            if (!AssetDatabase.DeleteAsset(AssetPath))
            {
                return ESVMCPCommandResult.Failed($"删除Asset失败: {AssetPath}");
            }

            return ESVMCPCommandResult.Succeed($"成功删除Asset: {AssetPath}");
        }

        private ESVMCPCommandResult ExecuteCopyAsset(ESVMCPExecutionContext context)
        {
            // 确保目标目录存在
            string targetDir = Path.GetDirectoryName(TargetPath);
            if (!AssetDatabase.IsValidFolder(targetDir))
            {
                return ESVMCPCommandResult.Failed($"目标文件夹不存在: {targetDir}");
            }

            if (!AssetDatabase.CopyAsset(AssetPath, TargetPath))
            {
                return ESVMCPCommandResult.Failed($"复制Asset失败: {AssetPath} -> {TargetPath}");
            }

            return ESVMCPCommandResult.Succeed($"成功复制Asset: {AssetPath} -> {TargetPath}");
        }

        private ESVMCPCommandResult ExecuteMoveAsset(ESVMCPExecutionContext context)
        {
            string error = AssetDatabase.MoveAsset(AssetPath, TargetPath);
            if (!string.IsNullOrEmpty(error))
            {
                return ESVMCPCommandResult.Failed($"移动Asset失败: {error}");
            }

            return ESVMCPCommandResult.Succeed($"成功移动Asset: {AssetPath} -> {TargetPath}");
        }

        private ESVMCPCommandResult ExecuteRenameAsset(ESVMCPExecutionContext context)
        {
            string error = AssetDatabase.RenameAsset(AssetPath, AssetName);
            if (!string.IsNullOrEmpty(error))
            {
                return ESVMCPCommandResult.Failed($"重命名Asset失败: {error}");
            }

            return ESVMCPCommandResult.Succeed($"成功重命名Asset: {AssetPath} -> {AssetName}");
        }

        private ESVMCPCommandResult ExecuteGetAssetPath(ESVMCPExecutionContext context)
        {
            // 从记忆或其他方式获取Object
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(AssetPath);
            if (obj == null)
            {
                return ESVMCPCommandResult.Failed($"未找到Asset: {AssetPath}");
            }

            string path = AssetDatabase.GetAssetPath(obj);
            return ESVMCPCommandResult.Succeed($"Asset路径: {path}", new Dictionary<string, object>
            {
                ["path"] = path
            });
        }

        private ESVMCPCommandResult ExecuteCreateFolder(ESVMCPExecutionContext context)
        {
            // 分割路径并逐级创建
            string[] pathParts = FolderPath.Split('/');
            string currentPath = "";
            
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (i == 0)
                {
                    currentPath = pathParts[0];
                }
                else
                {
                    string parentPath = currentPath;
                    string folderName = pathParts[i];
                    string newPath = currentPath + "/" + folderName;

                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(parentPath, folderName);
                    }
                    currentPath = newPath;
                }
            }

            AssetDatabase.Refresh();
            return ESVMCPCommandResult.Succeed($"成功创建文件夹: {FolderPath}");
        }

        private ESVMCPCommandResult ExecuteImportAsset(ESVMCPExecutionContext context)
        {
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            return ESVMCPCommandResult.Succeed($"成功导入Asset: {AssetPath}");
        }

        private ESVMCPCommandResult ExecuteRefreshAssets(ESVMCPExecutionContext context)
        {
            AssetDatabase.Refresh();
            return ESVMCPCommandResult.Succeed("成功刷新资源数据库");
        }

        private ESVMCPCommandResult ExecuteFindAssets(ESVMCPExecutionContext context)
        {
            string[] guids;
            
            if (!string.IsNullOrEmpty(FolderPath))
            {
                guids = AssetDatabase.FindAssets(SearchFilter, new[] { FolderPath });
            }
            else
            {
                guids = AssetDatabase.FindAssets(SearchFilter);
            }

            var results = new List<object>();
            int maxResults = System.Math.Min(guids.Length, 50); // 限制结果数量

            for (int i = 0; i < maxResults; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                
                if (asset != null)
                {
                    results.Add(new Dictionary<string, object>
                    {
                        ["name"] = asset.name,
                        ["path"] = path,
                        ["type"] = asset.GetType().Name,
                        ["guid"] = guids[i]
                    });
                }
            }

            return ESVMCPCommandResult.Succeed($"找到 {results.Count} 个Assets (共 {guids.Length} 个)", new Dictionary<string, object>
            {
                ["assets"] = results,
                ["totalCount"] = guids.Length
            });
        }
    }
}
