using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

namespace ES.VMCP
{
    // ==================== 模板 1: 基础命令模板 ====================
    // 适用于：固定参数的标准命令
    
    /// <summary>
    /// [命令描述] - 修改这里
    /// </summary>
    [ESVMCPCommand("TemplateCommand", "模板命令")]  // 修改命令名称和描述
    public class TemplateCommand : ESVMCPCommandBase
    {
        // 定义您的参数
        [JsonProperty("param1")]
        public string Param1 { get; set; }

        [JsonProperty("param2")]
        public int Param2 { get; set; }

        // 命令描述
        public override string Description => $"执行操作: {Param1}";

        // 执行逻辑
        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // === 在这里编写您的命令逻辑 ===
                
                Debug.Log($"执行命令: {Param1}, {Param2}");

                // 返回成功
                return ESVMCPCommandResult.Succeed("命令执行成功");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"命令执行失败: {e.Message}", e);
            }
        }

        // 参数验证（可选）
        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Param1))
            {
                return ESVMCPValidationResult.Failure("参数1不能为空");
            }
            return ESVMCPValidationResult.Success();
        }
    }


    // ==================== 模板 2: GameObject操作命令 ====================
    // 适用于：需要操作场景对象的命令
    
    /// <summary>
    /// GameObject操作命令模板
    /// </summary>
    [ESVMCPCommand("GameObjectTemplate", "GameObject模板")]
    public class GameObjectTemplateCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        public override string Description => $"操作对象: {Target}";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 解析GameObject
                GameObject go = TargetResolver.Resolve(Target, context);
                if (go == null)
                {
                    return ESVMCPCommandResult.Failed($"未找到GameObject: {Target}");
                }

                // === 对GameObject进行操作 ===
                
                // 示例：修改位置
                // go.transform.position = new Vector3(0, 1, 0);

                // 保存到记忆（可选）
                context.SceneMemory?.SaveGameObject(Id, go);

                return ESVMCPCommandResult.Succeed($"操作成功: {go.name}");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"操作失败: {e.Message}", e);
            }
        }
    }


    // ==================== 模板 3: 记忆系统命令 ====================
    // 适用于：需要存储/读取数据的命令
    
    /// <summary>
    /// 记忆系统命令模板
    /// </summary>
    [ESVMCPCommand("MemoryTemplate", "记忆模板")]
    public class MemoryTemplateCommand : ESVMCPCommandBase
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("longTerm")]
        public bool LongTerm { get; set; } = false;

        public override string Description => $"记忆操作: {Key}";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                if (context.SceneMemory == null)
                {
                    return ESVMCPCommandResult.Failed("记忆系统不可用");
                }

                // 保存记忆
            context.SceneMemory.Save(Key, Value, LongTerm);
                // 或读取记忆
                // object savedValue = context.SceneMemory.GetMemory(Key);

                return ESVMCPCommandResult.Succeed($"记忆已保存: {Key}");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"记忆操作失败: {e.Message}", e);
            }
        }
    }


    // ==================== 模板 4: 带输出数据的命令 ====================
    // 适用于：需要返回数据供后续命令使用
    
    /// <summary>
    /// 带输出数据的命令模板
    /// </summary>
    [ESVMCPCommand("OutputTemplate", "输出模板")]
    public class OutputTemplateCommand : ESVMCPCommandBase
    {
        [JsonProperty("input")]
        public string Input { get; set; }

        public override string Description => $"处理: {Input}";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // === 执行处理逻辑 ===
                
                // 准备输出数据
                var output = new Dictionary<string, object>
                {
                    { "result", "处理结果" },
                    { "timestamp", DateTime.Now.ToString() },
                    { "status", "success" }
                };

                // 后续命令可以通过 {{commandId.result}} 引用输出
                return ESVMCPCommandResult.Succeed("处理完成", output);
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"处理失败: {e.Message}", e);
            }
        }
    }


    // ==================== 模板 5: 变参数命令 ====================
    // 适用于：参数数量不固定的命令
    
    /// <summary>
    /// 变参数命令模板
    /// </summary>
    [ESVMCPCommand("VariableTemplate", "变参数模板")]
    public class VariableTemplateCommand : ESVMCPVariableCommand
    {
        [JsonProperty("mainParam")]
        public string MainParam { get; set; }

        public override string Description => $"灵活命令: {MainParam}";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                // 访问固定参数
                Debug.Log($"主参数: {MainParam}");

                // 访问可选参数（有默认值）
                int count = GetParameter<int>("count", 1);
                string mode = GetParameter<string>("mode", "default");
                bool flag = GetParameter<bool>("flag", false);

                // 检查参数是否存在
                if (HasParameter("optionalParam"))
                {
                    string optional = GetParameter<string>("optionalParam");
                    Debug.Log($"可选参数: {optional}");
                }

                // 遍历所有额外参数
                foreach (var key in GetParameterKeys())
                {
                    object value = GetParameter<object>(key);
                    Debug.Log($"参数 {key} = {value}");
                }

                return ESVMCPCommandResult.Succeed("执行完成");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"执行失败: {e.Message}", e);
            }
        }
    }


    // ==================== 模板 6: 危险操作命令 ====================
    // 适用于：删除、清空等不可逆操作
    
    /// <summary>
    /// 危险操作命令模板
    /// </summary>
    [ESVMCPCommand("DangerousTemplate", "危险操作模板", isDangerous: true)]
    public class DangerousTemplateCommand : ESVMCPCommandBase
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("confirm")]
        public bool Confirm { get; set; } = false;

        public override bool IsDangerous => true;

        public override string Description => $"危险操作: {Target}";

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            // 需要确认
            if (!Confirm)
            {
                return ESVMCPCommandResult.Failed("危险操作需要确认 (confirm=true)");
            }

            try
            {
                // === 执行危险操作 ===
                
                return ESVMCPCommandResult.Succeed("危险操作已执行");
            }
            catch (Exception e)
            {
                return ESVMCPCommandResult.Failed($"危险操作失败: {e.Message}", e);
            }
        }

        public override ESVMCPValidationResult Validate()
        {
            if (!Confirm)
            {
                return ESVMCPValidationResult.Failure("必须设置confirm=true以确认危险操作");
            }
            return ESVMCPValidationResult.Success();
        }
    }
}
