using UnityEngine;
using System;
using System.Collections.Generic;

namespace ES.Preview.SkillSystem
{
    /// <summary>
    /// 技能系统核心设计
    /// 
    /// **设计理念**：
    /// - SO-Based数据驱动
    /// - SkillEffect组件化
    /// - 与Link系统无缝集成
    /// - 支持技能冷却、打断、组合技
    /// </summary>
    /// 
    #region Skill Definition (ScriptableObject)
    
    /// <summary>
    /// 技能定义（ScriptableObject资产）
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Skill/SkillDefinition")]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Basic Info")]
        public string skillId;
        public string displayName;
        [TextArea(3, 5)]
        public string description;
        public Sprite icon;
        
        [Header("Skill Properties")]
        public float cooldown = 1f;              // 冷却时间
        public float castTime = 0f;              // 施法时间（0=瞬发）
        public float manaCost = 10f;             // 消耗
        public SkillTargetType targetType;       // 目标类型
        public float range = 10f;                // 施法距离
        
        [Header("Effects")]
        public List<SkillEffectData> effects;    // 技能效果列表
        
        [Header("Visual")]
        public GameObject castVfx;               // 施法特效
        public GameObject hitVfx;                // 命中特效
        public AudioClip castSound;              // 施法音效
        public AnimationClip castAnimation;      // 施法动画
        
        [Header("Advanced")]
        public bool canBeCanceled = true;        // 是否可被打断
        public List<string> comboSkills;         // 组合技列表
        public float comboCooldown = 2f;         // 组合技窗口期
    }
    
    public enum SkillTargetType
    {
        Self,           // 自身
        SingleTarget,   // 单体目标
        AOE,            // 范围（需要指定中心点）
        Direction       // 方向性（如冲锋、投射物）
    }
    
    /// <summary>
    /// 技能效果数据（序列化）
    /// </summary>
    [Serializable]
    public class SkillEffectData
    {
        public SkillEffectType effectType;
        public float value;                      // 数值（伤害/治疗量等）
        public float duration;                   // 持续时间（DOT/BUFF）
        public float tickInterval;               // Tick间隔
        
        [Header("Conditions")]
        public bool requiresTarget = true;
        public LayerMask affectedLayers;
        
        [Header("Visual")]
        public GameObject effectPrefab;          // 效果预制体
    }
    
    public enum SkillEffectType
    {
        Damage,                 // 伤害
        Heal,                   // 治疗
        Buff,                   // 增益
        Debuff,                 // 减益
        Summon,                 // 召唤
        Teleport,               // 传送
        Custom                  // 自定义（通过脚本实现）
    }
    
    #endregion
    
    #region Skill Runtime
    
    /// <summary>
    /// 技能运行时实例
    /// </summary>
    public class SkillInstance
    {
        public SkillDefinition Definition { get; private set; }
        public GameObject Owner { get; private set; }
        
        // 状态
        public SkillState State { get; private set; } = SkillState.Ready;
        public float RemainingCooldown { get; private set; }
        public float CastProgress { get; private set; }
        
        // 组合技
        private float comboTimer;
        private List<string> availableComboSkills = new();
        
        public SkillInstance(SkillDefinition definition, GameObject owner)
        {
            Definition = definition;
            Owner = owner;
        }
        
        public void Update(float deltaTime)
        {
            switch (State)
            {
                case SkillState.Cooldown:
                    RemainingCooldown -= deltaTime;
                    if (RemainingCooldown <= 0)
                    {
                        State = SkillState.Ready;
                        RemainingCooldown = 0;
                    }
                    break;
                    
                case SkillState.Casting:
                    CastProgress += deltaTime;
                    if (CastProgress >= Definition.castTime)
                    {
                        ExecuteSkill();
                        State = SkillState.Cooldown;
                        RemainingCooldown = Definition.cooldown;
                        CastProgress = 0;
                    }
                    break;
            }
            
            // 组合技窗口期
            if (comboTimer > 0)
            {
                comboTimer -= deltaTime;
                if (comboTimer <= 0)
                {
                    availableComboSkills.Clear();
                }
            }
        }
        
        public bool TryCast(Vector3 targetPosition = default, GameObject target = null)
        {
            if (State != SkillState.Ready)
                return false;
            
            // 检查条件
            if (!CheckCastConditions(target))
                return false;
            
            State = SkillState.Casting;
            CastProgress = 0;
            
            // 发送事件
            SkillSystemEvents.OnSkillCastStart?.Invoke(new SkillCastEvent
            {
                skill = Definition,
                caster = Owner,
                target = target,
                targetPosition = targetPosition
            });
            
            // 播放特效
            PlayCastEffects();
            
            // 瞬发技能
            if (Definition.castTime <= 0)
            {
                ExecuteSkill();
                State = SkillState.Cooldown;
                RemainingCooldown = Definition.cooldown;
            }
            
            return true;
        }
        
        public void Cancel()
        {
            if (State == SkillState.Casting && Definition.canBeCanceled)
            {
                State = SkillState.Ready;
                CastProgress = 0;
                
                SkillSystemEvents.OnSkillCanceled?.Invoke(new SkillCastEvent
                {
                    skill = Definition,
                    caster = Owner
                });
            }
        }
        
        private bool CheckCastConditions(GameObject target)
        {
            // 检查目标
            if (Definition.targetType == SkillTargetType.SingleTarget && target == null)
                return false;
            
            // 检查距离
            if (target != null)
            {
                float distance = Vector3.Distance(Owner.transform.position, target.transform.position);
                if (distance > Definition.range)
                    return false;
            }
            
            // 检查资源消耗（假设有ManaComponent）
            // var mana = Owner.GetComponent<ManaComponent>();
            // if (mana != null && mana.CurrentMana < Definition.manaCost)
            //     return false;
            
            return true;
        }
        
        private void ExecuteSkill()
        {
            // 执行所有效果
            foreach (var effectData in Definition.effects)
            {
                ExecuteEffect(effectData);
            }
            
            // 发送事件
            SkillSystemEvents.OnSkillExecuted?.Invoke(new SkillCastEvent
            {
                skill = Definition,
                caster = Owner
            });
            
            // 开启组合技窗口
            if (Definition.comboSkills != null && Definition.comboSkills.Count > 0)
            {
                availableComboSkills.Clear();
                availableComboSkills.AddRange(Definition.comboSkills);
                comboTimer = Definition.comboCooldown;
            }
        }
        
        private void ExecuteEffect(SkillEffectData effectData)
        {
            switch (effectData.effectType)
            {
                case SkillEffectType.Damage:
                    ApplyDamage(effectData);
                    break;
                case SkillEffectType.Heal:
                    ApplyHeal(effectData);
                    break;
                case SkillEffectType.Buff:
                    ApplyBuff(effectData);
                    break;
                // ... 其他类型
            }
        }
        
        private void ApplyDamage(SkillEffectData effectData)
        {
            // 查找范围内的目标
            Collider[] hits = Physics.OverlapSphere(
                Owner.transform.position,
                Definition.range,
                effectData.affectedLayers
            );
            
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<HealthComponent>();
                if (health != null)
                {
                    health.TakeDamage(effectData.value);
                    
                    // 生成特效
                    if (Definition.hitVfx != null)
                    {
                        GameObject.Instantiate(Definition.hitVfx, hit.transform.position, Quaternion.identity);
                    }
                }
            }
        }
        
        private void ApplyHeal(SkillEffectData effectData)
        {
            var health = Owner.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.Heal(effectData.value);
            }
        }
        
        private void ApplyBuff(SkillEffectData effectData)
        {
            var buffSystem = Owner.GetComponent<BuffSystemComponent>();
            if (buffSystem != null)
            {
                buffSystem.AddBuff(new BuffInstance
                {
                    duration = effectData.duration,
                    value = effectData.value
                });
            }
        }
        
        private void PlayCastEffects()
        {
            if (Definition.castVfx != null)
            {
                GameObject.Instantiate(Definition.castVfx, Owner.transform.position, Quaternion.identity);
            }
            
            if (Definition.castSound != null)
            {
                AudioSource.PlayClipAtPoint(Definition.castSound, Owner.transform.position);
            }
        }
        
        public bool CanComboInto(string skillId)
        {
            return availableComboSkills.Contains(skillId) && comboTimer > 0;
        }
    }
    
    public enum SkillState
    {
        Ready,      // 就绪
        Casting,    // 施法中
        Cooldown    // 冷却中
    }
    
    #endregion
    
    #region Skill Manager (Module)
    
    /// <summary>
    /// 技能管理模块（挂载到角色上）
    /// </summary>
    public class SkillManagerModule : BaseESModule
    {
        private Dictionary<string, SkillInstance> skills = new();
        private GameObject owner;

        public override bool HostEnable => throw new NotImplementedException();

        public SkillManagerModule(GameObject owner)
        {
            this.owner = owner;
        }
        
        public void LearnSkill(SkillDefinition skillDef)
        {
            if (!skills.ContainsKey(skillDef.skillId))
            {
                skills[skillDef.skillId] = new SkillInstance(skillDef, owner);
                
                SkillSystemEvents.OnSkillLearned?.Invoke(new SkillLearnEvent
                {
                    skillId = skillDef.skillId,
                    owner = owner
                });
            }
        }
        
        public bool CastSkill(string skillId, Vector3 targetPosition = default, GameObject target = null)
        {
            if (!skills.TryGetValue(skillId, out var skill))
            {
                Debug.LogWarning($"Skill not found: {skillId}");
                return false;
            }
            
            return skill.TryCast(targetPosition, target);
        }
        
        public void CancelCasting()
        {
            foreach (var skill in skills.Values)
            {
                if (skill.State == SkillState.Casting)
                {
                    skill.Cancel();
                    break;
                }
            }
        }
        
        protected override void Update()
        {
            foreach (var skill in skills.Values)
            {
                skill.Update(Time.deltaTime);
            }
        }
        
        public SkillInstance GetSkill(string skillId)
        {
            return skills.GetValueOrDefault(skillId);
        }
        
        public List<SkillInstance> GetAllSkills()
        {
            return new List<SkillInstance>(skills.Values);
        }

        public override void TryDestroySelf()
        {
            throw new NotImplementedException();
        }
    }
    
    #endregion
    
    #region Link System Integration
    
    /// <summary>
    /// 技能系统事件（通过Link分发）
    /// </summary>
    public static class SkillSystemEvents
    {
        public static Action<SkillCastEvent> OnSkillCastStart;
        public static Action<SkillCastEvent> OnSkillExecuted;
        public static Action<SkillCastEvent> OnSkillCanceled;
        public static Action<SkillLearnEvent> OnSkillLearned;
    }
    
    public struct SkillCastEvent
    {
        public SkillDefinition skill;
        public GameObject caster;
        public GameObject target;
        public Vector3 targetPosition;
    }
    
    public struct SkillLearnEvent
    {
        public string skillId;
        public GameObject owner;
    }
    
    #endregion
    
    #region Helper Components
    
    /// <summary>
    /// 健康组件（示例）
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float currentHealth = 100f;
        
        public void TakeDamage(float damage)
        {
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }
        
        public void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }
        
        private void Die()
        {
            // 死亡逻辑
            Debug.Log($"{gameObject.name} died");
        }
    }
    
    /// <summary>
    /// Buff 系统组件（示例）
    /// </summary>
    public class BuffSystemComponent : MonoBehaviour
    {
        private List<BuffInstance> activeBuffs = new();
        
        void Update()
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = activeBuffs[i];
                buff.duration -= Time.deltaTime;
                if (activeBuffs[i].duration <= 0)
                {
                    activeBuffs.RemoveAt(i);
                }
            }
        }
        
        public void AddBuff(BuffInstance buff)
        {
            activeBuffs.Add(buff);
        }
    }
    
    public struct BuffInstance
    {
        public float duration;
        public float value;
    }
    
    #endregion
}
