using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Obsolete("示例模块，建议放入具体域模块脚本中", false)]
    [Serializable, TypeRegistryItem("模拟健康模块")]
    public class EntityMockHealthModule : EntityBasicModuleBase
    {
        public int maxHealth = 100;
        public int currentHealth = 100;

        public override void Start()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        public bool ApplyDamage(int amount)
        {
            if (amount <= 0) return false;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            return true;
        }

        public bool Heal(int amount)
        {
            if (amount <= 0) return false;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            return true;
        }

        public bool IsDead()
        {
            return currentHealth <= 0;
        }
    }
}
