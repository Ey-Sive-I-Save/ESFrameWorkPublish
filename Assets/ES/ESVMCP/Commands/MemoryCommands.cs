using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ES.VMCP
{
    /// <summary>
    /// 保存到记忆命令
    /// </summary>
    [ESVMCPCommand("SaveMemory", "保存数据到记忆系统")]
    public class SaveMemoryCommand : ESVMCPCommandBase
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("persistent")]
        public bool Persistent { get; set; } = false; // false=场景记忆, true=持久记忆

        public override string Description => $"保存记忆: {Key}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Key))
            {
                return ESVMCPValidationResult.Failure("记忆键不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 解析变量引用
                string resolvedValue = Value?.ToString() ?? "";
                if (resolvedValue.Contains("{{"))
                {
                    resolvedValue = ResolveVariable(resolvedValue, context);
                }

                if (Persistent)
                {
                    // 保存到持久记忆
                    if (context.PersistentMemory != null)
                    {
                        context.PersistentMemory.SaveMemory(Key, resolvedValue);
                        return ESVMCPCommandResult.Succeed($"成功保存到持久记忆: {Key}");
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("持久记忆系统未初始化");
                    }
                }
                else
                {
                    // 保存到场景记忆
                    if (context.SceneMemory != null)
                    {
                        context.SceneMemory.SaveMemory(Key, resolvedValue);
                        return ESVMCPCommandResult.Succeed($"成功保存到场景记忆: {Key}");
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("场景记忆系统未初始化");
                    }
                }
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"保存记忆失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 从记忆加载命令
    /// </summary>
    [ESVMCPCommand("LoadMemory", "从记忆系统加载数据")]
    public class LoadMemoryCommand : ESVMCPCommandBase
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("persistent")]
        public bool Persistent { get; set; } = false;

        public override string Description => $"加载记忆: {Key}";

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Key))
            {
                return ESVMCPValidationResult.Failure("记忆键不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                object value = null;

                if (Persistent)
                {
                    if (context.PersistentMemory != null && context.PersistentMemory.HasMemory(Key))
                    {
                        value = context.PersistentMemory.GetMemory(Key);
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed($"持久记忆中未找到键: {Key}");
                    }
                }
                else
                {
                    if (context.SceneMemory != null && context.SceneMemory.HasMemory(Key))
                    {
                        value = context.SceneMemory.GetMemory(Key);
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed($"场景记忆中未找到键: {Key}");
                    }
                }

                var output = new Dictionary<string, object>
                {
                    { "key", Key },
                    { "value", value }
                };

                return ESVMCPCommandResult.Succeed($"成功加载记忆: {Key}", output);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"加载记忆失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 移除记忆命令
    /// </summary>
    [ESVMCPCommand("RemoveMemory", "从记忆系统移除数据", isDangerous: true)]
    public class RemoveMemoryCommand : ESVMCPCommandBase
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("persistent")]
        public bool Persistent { get; set; } = false;

        public override string Description => $"移除记忆: {Key}";
        public override bool IsDangerous => true;

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Key))
            {
                return ESVMCPValidationResult.Failure("记忆键不能为空");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                if (Persistent)
                {
                    if (context.PersistentMemory != null)
                    {
                        context.PersistentMemory.RemoveMemory(Key);
                        return ESVMCPCommandResult.Succeed($"成功从持久记忆移除: {Key}");
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("持久记忆系统未初始化");
                    }
                }
                else
                {
                    if (context.SceneMemory != null)
                    {
                        context.SceneMemory.RemoveMemory(Key);
                        return ESVMCPCommandResult.Succeed($"成功从场景记忆移除: {Key}");
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("场景记忆系统未初始化");
                    }
                }
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"移除记忆失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 清空记忆命令
    /// </summary>
    [ESVMCPCommand("ClearMemory", "清空记忆系统", isDangerous: true)]
    public class ClearMemoryCommand : ESVMCPCommandBase
    {
        [JsonProperty("persistent")]
        public bool Persistent { get; set; } = false;

        [JsonProperty("confirm")]
        public bool Confirm { get; set; } = false; // 安全确认

        public override string Description => Persistent ? "清空持久记忆" : "清空场景记忆";
        public override bool IsDangerous => true;

        public override ESVMCPValidationResult Validate()
        {
            if (!Confirm)
            {
                return ESVMCPValidationResult.Failure("危险操作需要confirm=true确认");
            }
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                if (Persistent)
                {
                    if (context.PersistentMemory != null)
                    {
                        int count = context.PersistentMemory.GetMemoryCount();
                        context.PersistentMemory.ClearMemory();
                        return ESVMCPCommandResult.Succeed($"成功清空持久记忆（{count}条数据）");
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("持久记忆系统未初始化");
                    }
                }
                else
                {
                    if (context.SceneMemory != null)
                    {
                        int count = context.SceneMemory.GetMemoryCount();
                        context.SceneMemory.ClearMemory();
                        return ESVMCPCommandResult.Succeed($"成功清空场景记忆（{count}条数据）");
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("场景记忆系统未初始化");
                    }
                }
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"清空记忆失败: {e.Message}", e);
            }
        }
    }

    /// <summary>
    /// 导出记忆命令
    /// </summary>
    [ESVMCPCommand("ExportMemory", "导出记忆系统数据")]
    public class ExportMemoryCommand : ESVMCPCommandBase
    {
        [JsonProperty("persistent")]
        public bool Persistent { get; set; } = false;

        [JsonProperty("format")]
        public string Format { get; set; } = "json"; // json 或 text

        public override string Description => "导出记忆数据";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                string exportData = "";

                if (Persistent)
                {
                    if (context.PersistentMemory != null)
                    {
                        if (Format == "json")
                            exportData = context.PersistentMemory.ExportToJson();
                        else
                            exportData = context.PersistentMemory.ExportToText();
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("持久记忆系统未初始化");
                    }
                }
                else
                {
                    if (context.SceneMemory != null)
                    {
                        if (Format == "json")
                            exportData = context.SceneMemory.ExportToJson();
                        else
                            exportData = context.SceneMemory.ExportToText();
                    }
                    else
                    {
                        return ESVMCPCommandResult.Failed("场景记忆系统未初始化");
                    }
                }

                var output = new Dictionary<string, object>
                {
                    { "exportData", exportData },
                    { "format", Format },
                    { "timestamp", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
                };

                return ESVMCPCommandResult.Succeed("成功导出记忆数据", output);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"导出记忆失败: {e.Message}", e);
            }
        }
    }
}
