using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

namespace ES.VMCP
{
    /// <summary>
    /// 记忆操作类型
    /// </summary>
    public enum MemoryOperation
    {
        Save,       // 保存记忆
        Load,       // 加载记忆
        Remove,     // 删除记忆
        Clear,      // 清除所有记忆
        Export,     // 导出记忆数据
        Has         // 检查记忆是否存在
    }

    /// <summary>
    /// 统一的记忆操作命令 - 合并所有记忆系统相关操作
    /// </summary>
    [ESVMCPCommand("MemoryOperation", "统一的记忆操作命令，支持保存、加载、删除、清除、导出等操作")]
    public class MemoryOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public MemoryOperation Operation { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

    [JsonProperty("longTerm")]
    public bool LongTerm { get; set; } = false;

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case MemoryOperation.Save:
                        return $"保存记忆: {Key} = {Value} [长期:{LongTerm}]";
                    case MemoryOperation.Load:
                        return $"加载记忆: {Key}";
                    case MemoryOperation.Remove:
                        return $"删除记忆: {Key}";
                    case MemoryOperation.Clear:
                        return "清除所有记忆";
                    case MemoryOperation.Export:
                        return "导出记忆数据";
                    case MemoryOperation.Has:
                        return $"检查记忆: {Key}";
                    default:
                        return $"记忆操作: {Operation}";
                }
            }
        }

        public override bool IsDangerous => Operation == MemoryOperation.Clear || Operation == MemoryOperation.Remove;

        public override ESVMCPValidationResult Validate()
        {
            switch (Operation)
            {
                case MemoryOperation.Save:
                    if (string.IsNullOrEmpty(Key))
                        return ESVMCPValidationResult.Failure("保存记忆时必须指定key参数");
                    break;

                case MemoryOperation.Load:
                case MemoryOperation.Remove:
                case MemoryOperation.Has:
                    if (string.IsNullOrEmpty(Key))
                        return ESVMCPValidationResult.Failure("操作记忆时必须指定key参数");
                    break;
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                if (context.SceneMemory == null)
                {
                    return ESVMCPCommandResult.Failed("记忆系统不可用");
                }

                switch (Operation)
                {
                    case MemoryOperation.Save:
                        return ExecuteSaveMemory(context);
                    case MemoryOperation.Load:
                        return ExecuteLoadMemory(context);
                    case MemoryOperation.Remove:
                        return ExecuteRemoveMemory(context);
                    case MemoryOperation.Clear:
                        return ExecuteClearMemory(context);
                    case MemoryOperation.Export:
                        return ExecuteExportMemory(context);
                    case MemoryOperation.Has:
                        return ExecuteHasMemory(context);
                    default:
                        return ESVMCPCommandResult.Failed($"不支持的操作类型: {Operation}");
                }
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"记忆操作失败: {e.Message}", e);
            }
        }

        private ESVMCPCommandResult ExecuteSaveMemory(ESVMCPExecutionContext context)
        {
            context.SceneMemory.Save(Key, Value, LongTerm);

            var output = new Dictionary<string, object>
            {
                { "key", Key },
                { "longTerm", LongTerm },
                { "timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };

            return ESVMCPCommandResult.Succeed($"记忆已保存: {Key}", output);
        }

        private ESVMCPCommandResult ExecuteLoadMemory(ESVMCPExecutionContext context)
        {
            object value = context.SceneMemory.Get(Key);

            if (value == null)
            {
                return ESVMCPCommandResult.Failed($"未找到记忆: {Key}");
            }

            var output = new Dictionary<string, object>
            {
                { "key", Key },
                { "value", value },
                { "found", true }
            };

            return ESVMCPCommandResult.Succeed($"记忆已加载: {Key}", output);
        }

        private ESVMCPCommandResult ExecuteRemoveMemory(ESVMCPExecutionContext context)
        {
            bool existed = context.SceneMemory.Has(Key);
            context.SceneMemory.Remove(Key);

            var output = new Dictionary<string, object>
            {
                { "key", Key },
                { "existed", existed }
            };

            return ESVMCPCommandResult.Succeed($"记忆已删除: {Key}", output);
        }

        private ESVMCPCommandResult ExecuteClearMemory(ESVMCPExecutionContext context)
        {
            int count = context.SceneMemory.TotalMemoryItems;
            context.SceneMemory.Clear();

            var output = new Dictionary<string, object>
            {
                { "cleared", count }
            };

            return ESVMCPCommandResult.Succeed($"已清除所有记忆，共 {count} 条", output);
        }

        private ESVMCPCommandResult ExecuteExportMemory(ESVMCPExecutionContext context)
        {
            var allKeys = context.SceneMemory.GetAllKeys();
            var gameObjectKeys = context.SceneMemory.GetKeysByType(ESVMCPMemoryItemType.GameObject);

            var output = new Dictionary<string, object>
            {
                { "allKeys", allKeys },
                { "gameObjectKeys", gameObjectKeys },
                { "exportTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "memoryCount", allKeys.Count },
                { "gameObjectCount", gameObjectKeys.Count }
            };

            return ESVMCPCommandResult.Succeed($"记忆数据已导出，共 {allKeys.Count} 条记忆，{gameObjectKeys.Count} 个GameObject", output);
        }

        private ESVMCPCommandResult ExecuteHasMemory(ESVMCPExecutionContext context)
        {
            bool exists = context.SceneMemory.Has(Key);

            var output = new Dictionary<string, object>
            {
                { "key", Key },
                { "exists", exists }
            };

            return ESVMCPCommandResult.Succeed($"记忆 {Key} {(exists ? "存在" : "不存在")}", output);
        }
    }
}