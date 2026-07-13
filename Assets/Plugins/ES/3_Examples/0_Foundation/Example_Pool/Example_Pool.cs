using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Sirenix.OdinInspector; // 添加 Odin Inspector 命名空间

namespace ES
{
    /// <summary>
    /// 示例池化对象：敌人
    /// 用于演示对象池中敌人的创建、重置和管理
    /// </summary>
    public class Example_Enemy : IPoolable
    {
        /// <summary>
        /// 敌人类型
        /// </summary>
        public string Type = "Enemy";

        /// <summary>
        /// 敌人血量
        /// </summary>
        public int Health = 100;

        /// <summary>
        /// 重置对象状态，准备回收到池中
        /// </summary>
        public void OnResetAsPoolable()
        {
            Health = 100; // 重置血量到初始值
        }

        /// <summary>
        /// 标记对象是否已被回收
        /// </summary>
        public bool IsRecycled { get; set; }

        public override string ToString()
        {
            return $"{Type} - Health: {Health}";
        }
    }

    /// <summary>
    /// 示例池化对象：子弹
    /// 用于演示对象池中子弹的创建、重置和管理
    /// </summary>
    public class Example_Bullet : IPoolable
    {
        /// <summary>
        /// 子弹类型
        /// </summary>
        public string Type = "Bullet";

        /// <summary>
        /// 子弹速度
        /// </summary>
        public float Speed = 10f;

        /// <summary>
        /// 重置对象状态，准备回收到池中
        /// </summary>
        public void OnResetAsPoolable()
        {
            Speed = 10f; // 重置速度到初始值
        }

        /// <summary>
        /// 标记对象是否已被回收
        /// </summary>
        public bool IsRecycled { get; set; }

        public override string ToString()
        {
            return $"{Type} - Speed: {Speed}";
        }
    }

    /// <summary>
    /// 示例池化对象：物品
    /// 用于演示对象池中物品的创建、重置和管理
    /// </summary>
    public class Example_Item : IPoolable
    {
        /// <summary>
        /// 物品类型
        /// </summary>
        public string Type = "Item";

        /// <summary>
        /// 物品名称
        /// </summary>
        public string Name = "Default Item";

        /// <summary>
        /// 重置对象状态，准备回收到池中
        /// </summary>
        public void OnResetAsPoolable()
        {
            Name = "Default Item"; // 重置名称到默认值
        }

        /// <summary>
        /// 标记对象是否已被回收
        /// </summary>
        public bool IsRecycled { get; set; }

        public override string ToString()
        {
            return $"{Type} - Name: {Name}";
        }
    }

    /// <summary>
    /// 对象池使用示例脚本
    /// 演示如何使用 ESSimplePool 创建和管理多个对象池，包括分组、统计和生命周期管理
    /// </summary>
    public class Example_Pool : MonoBehaviour
    {
        /// <summary>
        /// 敌人对象池
        /// </summary>
        private ESSimplePool<Example_Enemy> enemyPool;

        /// <summary>
        /// 子弹对象池
        /// </summary>
        private ESSimplePool<Example_Bullet> bulletPool;

        /// <summary>
        /// 物品对象池
        /// </summary>
        private ESSimplePool<Example_Item> itemPool;

        /// <summary>
        /// 当前活跃的敌人列表，用于管理生命周期
        /// </summary>
        private List<Example_Enemy> activeEnemies = new List<Example_Enemy>();

        /// <summary>
        /// 当前活跃的子弹列表，用于管理生命周期
        /// </summary>
        private List<Example_Bullet> activeBullets = new List<Example_Bullet>();

        /// <summary>
        /// 当前活跃的物品列表，用于管理生命周期
        /// </summary>
        private List<Example_Item> activeItems = new List<Example_Item>();

        /// <summary>
        /// 初始化对象池和开始模拟
        /// </summary>
        void Start()
        {
            // 初始化敌人池
            enemyPool = new ESSimplePool<Example_Enemy>(
                () => new Example_Enemy { Type = "Orc" }, // 工厂方法：创建兽人敌人
                null, // 重置方法：使用默认重置
                5, // 初始预热数量
                50, // 最大容量
                onCreate: (e) => Debug.Log($"Created Enemy: {e}"), // 创建回调
                onDestroy: (e) => Debug.Log($"Destroyed Enemy: {e}") // 销毁回调
            );
            enemyPool.SetGroupName("游戏通用核心"); // 设置分组名称，便于统计管理
            enemyPool.SetDisplayName("兽人敌人池"); // 设置显示名称
            enemyPool.Prewarm(10); // 额外预热10个对象

            // 初始化子弹池
            bulletPool = new ESSimplePool<Example_Bullet>(
                () => new Example_Bullet { Type = "Plasma" }, // 工厂方法：创建等离子子弹
                null,
                10, // 初始数量
                100, // 最大容量
                onCreate: (b) => Debug.Log($"Created Bullet: {b}"),
                onDestroy: (b) => Debug.Log($"Destroyed Bullet: {b}")
            );
            bulletPool.SetGroupName("游戏通用核心"); // 分组：投射物-子弹
            bulletPool.SetDisplayName("等离子子弹池");
            bulletPool.SetMaxCount(200, true); // 设置最大容量为200

            // 初始化物品池
            itemPool = new ESSimplePool<Example_Item>(
                () => new Example_Item { Type = "Health Potion" }, // 工厂方法：创建生命药水
                null,
                3, // 初始数量
                30, // 最大容量
                onCreate: (i) => Debug.Log($"Created Item: {i}"),
                onDestroy: (i) => Debug.Log($"Destroyed Item: {i}")
            );
            itemPool.SetGroupName("游戏通用核心"); // 分组：物品-消耗品
            itemPool.SetDisplayName("生命药水池");

            // 开始协程模拟对象池的使用
            StartCoroutine(SimulatePoolUsage());
        }

        /// <summary>
        /// 协程：模拟对象池的使用，包括定时获取和放回对象
        /// </summary>
        private IEnumerator SimulatePoolUsage()
        {
            while (true)
            {
                // 每2秒获取一组对象
                yield return new WaitForSeconds(2f);

                // 获取敌人
                var enemy = enemyPool.GetInPool();
                activeEnemies.Add(enemy);
                Debug.Log($"Got Enemy: {enemy}");

                // 获取子弹
                var bullet = bulletPool.GetInPool();
                activeBullets.Add(bullet);
                Debug.Log($"Got Bullet: {bullet}");

                // 获取物品
                var item = itemPool.GetInPool();
                activeItems.Add(item);
                Debug.Log($"Got Item: {item}");

                // 3秒后放回对象
                yield return new WaitForSeconds(3f);

                // 放回敌人
                if (activeEnemies.Count > 0)
                {
                    var enemyToReturn = activeEnemies[0];
                    activeEnemies.RemoveAt(0);
                    enemyPool.PushToPool(enemyToReturn);
                    Debug.Log($"Returned Enemy: {enemyToReturn}");
                }

                // 放回子弹
                if (activeBullets.Count > 0)
                {
                    var bulletToReturn = activeBullets[0];
                    activeBullets.RemoveAt(0);
                    bulletPool.PushToPool(bulletToReturn);
                    Debug.Log($"Returned Bullet: {bulletToReturn}");
                }

                // 放回物品
                if (activeItems.Count > 0)
                {
                    var itemToReturn = activeItems[0];
                    activeItems.RemoveAt(0);
                    itemPool.PushToPool(itemToReturn);
                    Debug.Log($"Returned Item: {itemToReturn}");
                }

                // 每10秒打印一次统计信息
                if (Time.time % 10 < 2)
                {
                    PrintStatistics();
                }
            }
        }

        /// <summary>
        /// 打印所有池的统计信息
        /// </summary>
        private void PrintStatistics()
        {
            Debug.Log("=== Pool Statistics ===");
            Debug.Log($"Enemy Pool: {enemyPool.GetStatistics()}");
            Debug.Log($"Bullet Pool: {bulletPool.GetStatistics()}");
            Debug.Log($"Item Pool: {itemPool.GetStatistics()}");
        }

        /// <summary>
        /// 每帧更新：处理用户输入以演示动态功能
        /// </summary>
        void Update()
        {
            // 按G键更改敌人池的分组
            if (Input.GetKeyDown(KeyCode.G))
            {
                enemyPool.SetGroupName("Combat_Bosses"); // 更改分组到Boss
                Debug.Log("Changed enemy pool group to Combat_Bosses");
            }

            // 按V键切换敌人池的可见性
            if (Input.GetKeyDown(KeyCode.V))
            {
                enemyPool.SetVisibility(false); // 隐藏统计
                Debug.Log("Set enemy pool visibility to false");
            }
        }

        /// <summary>
        /// 销毁时清理所有对象池
        /// </summary>
        void OnDestroy()
        {
            // 清理池以释放资源
            enemyPool?.Clear();
            bulletPool?.Clear();
            itemPool?.Clear();
        }

        // ===== Odin Inspector 按钮方法 =====

        /// <summary>
        /// 获取一个敌人对象（按钮）
        /// </summary>
        [Button("获取敌人", ButtonSizes.Medium)]
        public void GetEnemy()
        {
            if (enemyPool != null)
            {
                var enemy = enemyPool.GetInPool();
                activeEnemies.Add(enemy);
                Debug.Log($"Manually Got Enemy: {enemy}");
            }
        }

        /// <summary>
        /// 放回一个敌人对象（按钮）
        /// </summary>
        [Button("放回敌人", ButtonSizes.Medium)]
        public void ReturnEnemy()
        {
            if (activeEnemies.Count > 0)
            {
                var enemy = activeEnemies[0];
                activeEnemies.RemoveAt(0);
                enemyPool.PushToPool(enemy);
                Debug.Log($"Manually Returned Enemy: {enemy}");
            }
            else
            {
                Debug.Log("No active enemies to return.");
            }
        }

        /// <summary>
        /// 获取一个子弹对象（按钮）
        /// </summary>
        [Button("获取子弹", ButtonSizes.Medium)]
        public void GetBullet()
        {
            if (bulletPool != null)
            {
                var bullet = bulletPool.GetInPool();
                activeBullets.Add(bullet);
                Debug.Log($"Manually Got Bullet: {bullet}");
            }
        }

        /// <summary>
        /// 放回一个子弹对象（按钮）
        /// </summary>
        [Button("放回子弹", ButtonSizes.Medium)]
        public void ReturnBullet()
        {
            if (activeBullets.Count > 0)
            {
                var bullet = activeBullets[0];
                activeBullets.RemoveAt(0);
                bulletPool.PushToPool(bullet);
                Debug.Log($"Manually Returned Bullet: {bullet}");
            }
            else
            {
                Debug.Log("No active bullets to return.");
            }
        }

        /// <summary>
        /// 获取一个物品对象（按钮）
        /// </summary>
        [Button("获取物品", ButtonSizes.Medium)]
        public void GetItem()
        {
            if (itemPool != null)
            {
                var item = itemPool.GetInPool();
                activeItems.Add(item);
                Debug.Log($"Manually Got Item: {item}");
            }
        }

        /// <summary>
        /// 放回一个物品对象（按钮）
        /// </summary>
        [Button("放回物品", ButtonSizes.Medium)]
        public void ReturnItem()
        {
            if (activeItems.Count > 0)
            {
                var item = activeItems[0];
                activeItems.RemoveAt(0);
                itemPool.PushToPool(item);
                Debug.Log($"Manually Returned Item: {item}");
            }
            else
            {
                Debug.Log("No active items to return.");
            }
        }

        /// <summary>
        /// 打印所有池的统计信息（按钮）
        /// </summary>
        [Button("打印统计", ButtonSizes.Large)]
        public void PrintStats()
        {
            PrintStatistics();
        }

        /// <summary>
        /// 更改敌人池分组为 Boss（按钮）
        /// </summary>
        [Button("更改分组为Boss", ButtonSizes.Medium)]
        public void ChangeGroupToBoss()
        {
            if (enemyPool != null)
            {
                enemyPool.SetGroupName("Combat_Bosses");
                Debug.Log("Changed enemy pool group to Combat_Bosses");
            }
        }

        /// <summary>
        /// 切换敌人池的可见性（按钮）
        /// </summary>
        [Button("切换可见性", ButtonSizes.Medium)]
        public void ToggleVisibility()
        {
            if (enemyPool != null)
            {
                bool currentVisibility = enemyPool.GetStatistics().IsValid;
                enemyPool.SetVisibility(!currentVisibility);
                Debug.Log($"Toggled enemy pool visibility to {!currentVisibility}");
            }
        }

        /// <summary>
        /// 清理所有池（按钮）
        /// </summary>
        [Button("清理所有池", ButtonSizes.Large)]
        public void ClearAllPools()
        {
            enemyPool?.Clear();
            bulletPool?.Clear();
            itemPool?.Clear();
            activeEnemies.Clear();
            activeBullets.Clear();
            activeItems.Clear();
            Debug.Log("Cleared all pools and active lists.");
        }

        /// <summary>
        /// 预热敌人池（按钮）
        /// </summary>
        [Button("预热敌人池", ButtonSizes.Medium)]
        public void PrewarmEnemyPool()
        {
            if (enemyPool != null)
            {
                enemyPool.Prewarm(5);
                Debug.Log("Prewarmed enemy pool with 5 objects.");
            }
        }

        /// <summary>
        /// 预热子弹池（按钮）
        /// </summary>
        [Button("预热子弹池", ButtonSizes.Medium)]
        public void PrewarmBulletPool()
        {
            if (bulletPool != null)
            {
                bulletPool.Prewarm(10);
                Debug.Log("Prewarmed bullet pool with 10 objects.");
            }
        }

        /// <summary>
        /// 预热物品池（按钮）
        /// </summary>
        [Button("预热物品池", ButtonSizes.Medium)]
        public void PrewarmItemPool()
        {
            if (itemPool != null)
            {
                itemPool.Prewarm(3);
                Debug.Log("Prewarmed item pool with 3 objects.");
            }
        }
    }
}
