using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class TestContainer : MonoBehaviour
    {
        #region List
        
        public List<int> ints=new List<int>();
        public SafeBasicList<int> safeBasicList = new SafeBasicList<int>();
        public SafeNormalList<int> safeNormalList = new SafeNormalList<int>();
        public SafeThreadBasicList<int> safeThreadBasicList = new SafeThreadBasicList<int>();
        public SafeThreadNormalList<int> safeThreadNormalList = new SafeThreadNormalList<int>();


        void MethodsForList()
        {
            // 示例（SafeList）简介：
            // 目标：演示 SafeBasicList / SafeNormalList / SafeThreadBasicList / SafeThreadNormalList 的常用操作与遍历模式。
            // 说明：这些容器支持缓冲写入以允许在遍历期间安全入队/出队，最终通过 ApplyBuffers/TryApplyBuffers 提交变更。

            // --- SafeBasicList（非线程安全、可见 OnChange 回调）
            // 注册变化回调（可用于 UI 更新或日志）
            safeBasicList.OnChange = (isAdd, item) => Debug.Log($"SafeBasicList OnChange: {(isAdd?"Add":"Remove")} {item}");
            // 单项/批量入队
            safeBasicList.Add(1);
            safeBasicList.AddRange(new[] { 2, 3, 4 });
            // 单项/批量出队
            safeBasicList.Remove(2);
            safeBasicList.RemoveRange(new[] { 3 });
            // 在遍历时不会立刻修改 ValuesNow（需要 ApplyBuffers 提交）
            foreach (var v in safeBasicList)
            {
                // 读取当前生效集合
            }
            // 提交缓冲并触发回调
            safeBasicList.ApplyBuffers();
         
            // --- SafeNormalList（脏标记 + 队列缓冲）
            
            safeNormalList.Add(10);
            safeNormalList.AddRange(new List<int> { 11, 12 });
            safeNormalList.Remove(11);
            // 检查是否存在（会考虑缓冲）
            if (safeNormalList.Contains(12)) { }
            // 提交缓冲（有 isDirty 优化）
            safeNormalList.ApplyBuffers();
            // foreach 遍历当前生效元素
            foreach (var v in safeNormalList)
            {
                // 处理 v
            }

            // --- SafeThreadBasicList（基本线程安全；内部通过锁保护缓冲）
            // 在其他线程中也可调用 TryAdd/TryRemove
            safeThreadBasicList.Add(100);
            safeThreadBasicList.AddRange(new[] { 101, 102 });
            safeThreadBasicList.Remove(101);
            // 安全提交（内部已使用锁）
            safeThreadBasicList.ApplyBuffers();

            // 线程安全遍历（读取 ValuesNow）
            foreach (var v in safeThreadBasicList)
            {
                // 读取 v（注意：ValuesNow 是普通 List，若跨线程访问需额外同步/快照）
            }

            // --- SafeThreadNormalList（脏标记 + 线程安全队列）
            safeThreadNormalList.Add(200);
            safeThreadNormalList.AddRange(new List<int> { 201, 202 });
            safeThreadNormalList.Remove(201);
            //ApplyBuffers 不考虑  isDirty 
            // TryApplyBuffers 会根据 isDirty 快速返回，适合每帧调用
            safeThreadNormalList.ApplyBuffers();
            safeThreadNormalList.ApplyBuffers();

            foreach (var v in safeThreadNormalList)
            {
                // 读取 v
            }
            
            safeBasicList.AutoApplyBuffers=true;
            safeNormalList.AutoApplyBuffers=true;
            safeThreadBasicList.AutoApplyBuffers=true;
            safeThreadNormalList.AutoApplyBuffers=true;
            //AutoApplyBuffers 让 每次使用 foreach 前自动提交缓冲，简化使用
            //否则需要用户自己完成 ApplyBuffers/TryApplyBuffers 调用
            

            // 小结（SafeList）：
            // - 使用 Add/Remove/AddRange/RemoveRange 入队变更，使用 ApplyBuffers/TryApplyBuffers 提交（旧 Try* API 已弃用）。
            // - 对于需要 UI 或日志的场景，可在 Basic 版使用 OnChange 回调。
            // - 线程安全版本在跨线程调用缓冲方法时更安全，但读取 ValuesNow 跨线程仍需谨慎或使用快照。
        }
        #endregion/*  */ 

        #region KeyGroup
        public KeyGroup<string, int> keyGroup = new KeyGroup<string, int>();
        public SafeKeyGroup<string, int> safeKeyGroup = new SafeKeyGroup<string, int>();


        public TypeMatchKeyGroup<ScriptableObject> typeMatchKeyGroup = new TypeMatchKeyGroup<ScriptableObject>();
        public SafeTypeMatchKeyGroup<ScriptableObject> safeTypeMatchKeyGroup = new SafeTypeMatchKeyGroup<ScriptableObject>();
        void MethodsForKeyGroup()
        {
            // 示例（KeyGroup）简介：
            // 目标：展示 KeyGroup 与 SafeKeyGroup 的常用操作（单/批量添加、删除、获取、自动创建与缓冲应用）。
            // 说明：最后演示类型匹配的 KeyGroup 用法，展示按类型索引与转换的能力。
            // 下面为示例代码：

            #region  常规的
            // 添加单个元素
            keyGroup.Add("A", 1);
             keyGroup.Add("A", 2);
            safeKeyGroup.Add("B", 2);

            // 批量添加
            keyGroup.AddRange("A", new List<int>() { 3, 4, 5 });
            safeKeyGroup.AddRange("B", new List<int>() { 6, 7, 8 });

            // 添加重复/覆盖示例
            keyGroup.Add("A", 555);
            safeKeyGroup.Add("B", 277);

            // 删除单个
            keyGroup.Remove("A", 1);
            safeKeyGroup.Remove("B", 2);

            // 批量删除
            keyGroup.RemoveRange("A", new List<int>() { 555 });
            safeKeyGroup.RemoveRange("B", new List<int>() { 277 });

            // 获取组并清空示例（直接访问底层组）
            var group = keyGroup.GetGroupDirectly("A");
            group.Clear();

            var group2 = safeKeyGroup.GetGroupDirectly("B");
            group2.Clear();

            // 自动创建开关示例（影响访问不存在键时的行为）
            keyGroup.SetAutoCreateOnAccess(false);
            safeKeyGroup.SetAutoCreateOnAccess(true);

            var groupNULL1 = keyGroup.GetGroupDirectly("不存在的键");
            var groupNULL2 = safeKeyGroup.GetGroupDirectly("不存在的键");

            // 查询是否包含
            if (keyGroup.TryContains("A", 3))
            {
                // true
            }

            if (safeKeyGroup.TryContains("B", 6))
            {
                // true
            }

            // 应用缓冲区（安全版本）
            safeKeyGroup.ApplyBuffers();
            #endregion

            #region  类型匹配的
            // 类型匹配 KeyGroup 使用 Type 作为 Select/Key 的示例
            typeMatchKeyGroup.Add<ScriptableObject>(ScriptableObject.CreateInstance<ScriptableObject>());
            safeTypeMatchKeyGroup.Add<ScriptableObject>(ScriptableObject.CreateInstance<ScriptableObject>());

            typeMatchKeyGroup.Remove<ScriptableObject>(ScriptableObject.CreateInstance<ScriptableObject>());
            safeTypeMatchKeyGroup.Remove<ScriptableObject>(ScriptableObject.CreateInstance<ScriptableObject>());

            var groupType = typeMatchKeyGroup.GetGroupAsIEnumable<ScriptableObject>();
            var groupType2 = safeTypeMatchKeyGroup.GetGroupAsIEnumable<ScriptableObject>();

            // 获取指定类型的新列表（类型匹配特性）
            List<ESSO> newList = typeMatchKeyGroup.GetNewGroupOfType<ESSO>();
            List<ESSO> newList2 = safeTypeMatchKeyGroup.GetNewGroupOfType<ESSO>();
            #endregion

            // 小结（KeyGroup）：
            // - KeyGroup 提供分组管理能力，支持按键批量增删与直接访问底层组。
            // - SafeKeyGroup 在并发或延迟场景下提供缓冲机制，使用 TryApplyBuffers/ApplyBuffers 提交变更。
        }
        

        #endregion

        #region  Dictionary
        public SafeDictionary<string, int> safeDictionary = new SafeDictionary<string, int>();
        public BidirectionalDictionary<string, int> bidirectionalDictionary = new BidirectionalDictionary<string, int>();

        public SelectDic<string, string, int> select = new SelectDic<string, string, int>();

        void MethodsForDictionary()
        {
            #region  SafeDictionary
            // 示例（SafeDictionary）简介：
            // 目标：展示 SafeDictionary 的安全读取与写入、默认工厂、自动创建行为与删除操作。
            // 下面为示例代码：

            // SafeDictionary
            // 1) 添加
            safeDictionary.Add("A", 1, overwriteIfExists: true);

            // 2) 读取（安全读取）
            int value1 = safeDictionary.GetValueSafe("A");
            int value2 = safeDictionary.GetValueSafe("A", defaultValue: 100);

            // 3) 索引器读取/写入（更简洁）
            int value3 = safeDictionary["A"];
            safeDictionary["A"] = 200;

            // 4) 默认值工厂与自动创建行为
            safeDictionary.SetDefaultValueFactory(() => -1);
            // 默认工厂设置为每次返回 -1

            safeDictionary.SetAutoCreateOnAccess(true);
            int value4 = safeDictionary.GetValueSafe("不存在的键");
            // 如果启用自动创建，访问不存在的键会在字典中创建该键并返回默认值

            int value5 = safeDictionary.GetValueSafe("不存在的键2", defaultValue: 100);
            // 显式的默认值会覆盖工厂/自动创建时的返回值

            // 5) 删除
            safeDictionary.Remove("A");

            // 小结（SafeDictionary）：
            // - 使用 GetValueSafe 与默认工厂可避免对不存在键的异常访问。
            // - SetAutoCreateOnAccess 启用后会在访问时创建默认值，谨慎使用以免意外增加键。
            #endregion

            #region  BidirectionalDictionary
            // 示例（BidirectionalDictionary）简介：
            // 目标：展示双向字典的添加/批量添加、通过值查找键、TryAdd 避免重复、双向查询与枚举、删除与清空。
            // 注意：值必须唯一以确保可逆映射。
            // 下面为示例代码：

            // BidirectionalDictionary - 双向映射（Key <-> Value）

            // 1) 添加（索引器/方法）
            bidirectionalDictionary["Key1"] = 123;
            bidirectionalDictionary.Add("Key2", 456);
            bidirectionalDictionary.Add(new KeyValuePair<string, int>("Key4", 789));
            bidirectionalDictionary.Add(new BidirectionalDictionary<string, int>.KeyValuePairInternal("Key4", 789));

            

            // 3) 通过值查找键
            string key = bidirectionalDictionary.GetKey(123);

            // 4) TryAdd（避免重复值）
            if (bidirectionalDictionary.TryAdd("Key2", 456))
            {
                // 添加成功
            }
            if (!bidirectionalDictionary.TryAdd("Key3", 456))
            {
                // 添加失败（值已存在）
            }

            // 5) 包含检查与 TryGet（双向）
            if (bidirectionalDictionary.ContainsKey("Key1")) { }
            if (bidirectionalDictionary.ContainsValue(123)) { }
            if (bidirectionalDictionary.TryGetValue("Key1", out var val)) { }
            if (bidirectionalDictionary.TryGetKey(123, out var k)) { }

            // 6) 枚举（正向/反向）
            foreach (var item in bidirectionalDictionary)
            {
                string k1 = item.Key;
                int v1 = item.Value;
            }
            var enumerator = bidirectionalDictionary.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string k2 = enumerator.Current.Key;
                int v2 = enumerator.Current.Value;
            }
            var enumeratorInv = bidirectionalDictionary.GetEnumeratorInverse();
            while (enumeratorInv.MoveNext())
            {
                string value_ = enumeratorInv.Current.Value;
                int key_ = enumeratorInv.Current.Key;
            }

            // 7) 删除（按键或按值）
            bidirectionalDictionary.Remove("Key1");
            bidirectionalDictionary.RemoveByValue(123);

            // 8) 清空
            bidirectionalDictionary.Clear();

            // 小结（BidirectionalDictionary）：
            // - 双向查询提供通过值反向查找键的能力，适用于需要反向索引的场景。
            // - 添加时必须保证值唯一；序列化时若不同键映射相同值会导致信息丢失。
            #endregion

            #region SelectDictionary

            // 示例（SelectDic）简介：
            // 目标：展示 SelectDic 的单/批量添加、索引器、查询、遍历、删除与清空等常用操作。
            // 说明：SelectDic 以 (Select, Key) 对定位 Element，常用于按分组查找的场景。
            // 下面为示例代码：

            //SelectDictionary
            //功能: 支持双键映射的容器，可以通过两个键来存储和查询唯一的值
            //SelectDic<K1, K2, V> 是一个分组（Select）+键（Key）-> 值（Element）的容器

            // 1) 基础添加
            select.Add("FirstKey", "SecondKey", 999);
            select.Add("FirstKey", "AnotherKey", 888);
            select.Add("DifferentKey", "SecondKey", 777);

            // 2) 单项添加或覆盖（AddOrSet）
            select.AddOrSet("FirstKey", "SecondKey", 4321);

            // 3) 批量添加/更新
            var addRangeItems = new List<KeyValuePair<string, int>>()
            {
                new KeyValuePair<string, int>("Key1", 111),
                new KeyValuePair<string, int>("Key2", 222),
                new KeyValuePair<string, int>("Key3", 333),
            };
            select.AddOrSetRange("BatchKey", addRangeItems);

            // 4) 批量添加仅当不存在（AddRangeOnly）
            var addOnlyItems = new List<KeyValuePair<string, int>>()
            {
                new KeyValuePair<string, int>("OnlyKey1", 444),
                new KeyValuePair<string, int>("OnlyKey2", 555),
            };
            select.AddRangeOnly("OnlyKey", addOnlyItems);

            // 5) 索引器（更简洁的读写方式）
            select["IndexKey1", "IndexKey2"] = 666; // 设置（不存在时自动创建）
            int indexValue = select["IndexKey1", "IndexKey2"]; // 获取（不存在时返回默认值）
            select["FirstKey", "SecondKey"] = 1000; // 更新已存在的值

            // 6) 查询
            int value = select.GetElement("FirstKey", "SecondKey");
            if (select.TryGetElement("FirstKey", "SecondKey", out var result))
            {
                int resultValue = result;
            }
            bool contains = select.Contains("FirstKey", "SecondKey");

            // 7) 获取某个 Select 下的所有键并遍历
            var keys = select.GetKeys("BatchKey");
            if (keys != null)
            {
                foreach (var k1 in keys)
                {
                    // 处理键 k
                }
            }

            // 8) 获取子字典并遍历（GetDic）
            var subDic = select.GetDic("FirstKey");
            foreach (var kv in subDic)
            {
                string subKey = kv.Key;
                int subValue = kv.Value;
            }

            // 9) 删除
            select.Remove("FirstKey", "SecondKey");

            // 10) 批量删除
            var removeKeys = new List<string> { "SecondKey", "AnotherKey" };
            select.RemoveRange("FirstKey", removeKeys);
            if (select.TryRemoveRange("FirstKey", new List<string> { "Key1", "Key2" }))
            {
                // 批量删除成功
            }

            // 11) 清空指定 Select
            select.ClearSelect("BatchKey");

            // 12) 清空所有数据（Clear）
            select.Clear();

            // 小结（SelectDic）：
            // - SelectDic 提供分组+键的双重索引方式，既可按分组遍历，也可高效定位单项。
            // - 提供索引器与 Try 系列方法，索引器用于简洁读写，Try 方法用于安全访问。
            // - 批量操作（AddOrSetRange/AddRangeOnly）与 GetDic/GetKeys 提供更高效的数据管理手段。

            #endregion


        }

        #endregion

        #region  Special Container

        public VersionedList<int> versionedList = new VersionedList<int>();
        public MirrorSync<int> mirrorSyncList = new MirrorSync<int>();
        
        // 示例（VersionedList & MirrorSync）
        // 目标：展示版本化列表的缓冲添加/移除、版本记录与镜像同步的常用操作。
        // 说明：VersionedList 维持 ValuesNow 和缓冲队列（Add/Remove），通过 ApplyBuffers/TryApplyBuffers 提交。
        //       MirrorSync 可绑定到 VersionedList，使用 SyncChanges 拉取变更记录。
        void MethodsForSpecialContainer()
        {
            // VersionedList - 基本缓冲添加/移除
            // 注意：下面的 Add/Remove 方法带有默认参数 `versionRecord = true`。
            //       调用 `versionedList.Add(10)` 等价于 `versionedList.Add(10, true)`，
            //       默认会在版本记录中写入该变更以供 MirrorSync 使用。
            //       若只想在列表端修改但不记录版本（例如镜像端本地修改），可传入 `false`：
            //       `versionedList.Add(10, false)`。
            //       AddRange/RemoveRange 不带 `versionRecord` 参数，若需要版本记录请逐项调用 Add/Remove 或手动使用 VersionItem。
            versionedList.Add(10);
            versionedList.AddRange(new List<int> { 20, 30 });
            versionedList.Remove(10);
            versionedList.RemoveRange(new List<int> { 20 });

            // 提交缓冲（默认 AutoApplyBuffers 为 true，可按需设置）
            versionedList.SetAutoApplyBuffers(false);
            versionedList.ApplyBuffers();
            // 或者：versionedList.ApplyBuffersIfDirty();

            // 版本记录操作（记录变更以供镜像同步）
            // 注意：`VersionItem` 为内部方法，不推荐外部直接调用。
            // 推荐使用带 `versionRecord` 参数的 `Add/Remove`，或在特殊场景下手动调用 `UpdateVersion()`。
            // 例如：`versionedList.Add(40)` 会同时将变更记录到版本中；若不想记录可传 `false`：`versionedList.Add(40, false)`。
            versionedList.UpdateVersion();

            // 查询/检查
            if (versionedList.Contains(30))
            {
                // 已在缓冲或当前列表中
            }

            // MirrorSync - 绑定并同步
            mirrorSyncList.Source = versionedList; // 将镜像的源设置为主列表
            mirrorSyncList.UpdateVersionToMaxOnly();

            // 使用 SyncChanges 拉取从上次版本到当前的变更记录
            foreach (var change in mirrorSyncList.SyncChanges())
            {
                bool isAdd = change.IsAdd;
                int v = change.value;
            }

            // 镜像直接在镜像端进行添加/移除（忽略版本或按策略记录）
            mirrorSyncList.Add_IgnoreVersion(50);
            mirrorSyncList.Remove_IgnoreVersion(50);

            // 将镜像注册为验证器，使主列表知道有镜像存在
            var validator = new MirrorSyncListValidator<int>(mirrorSyncList, () => true);
            versionedList.BindMirrorValidators(validator);

            // 小结：
            // - VersionedList 通过缓冲队列提升批量更新性能，使用 ApplyBuffers/TryApplyBuffers 提交。
            // - 使用 VersionItem/TryUpdateVersion 可记录变更并与 MirrorSync 配合实现增量同步。
            // - MirrorSync.SyncChanges 返回自上次版本以来的变更记录，可在镜像端逐条应用以保持一致性。
        }
        
        
        #endregion

        #region VersionedList 深度示例

        // 深度解析与示例：展示版本化列表与镜像的完整同步流程、冲突策略与性能注意点
        void MethodsForVersionedMirrorAdvanced()
        {
            // 场景说明：主列表负责写操作并记录版本增量；镜像周期性拉取增量并应用以保持一致性。
            // 关键点：
            // - Add/Remove 默认会将变更写入版本记录（versionRecord = true），可通过传 false 跳过记录（常用于镜像端本地修改）。
            // - 操作入队按时间顺序保留（内部 opBuffer），ApplyBuffers 按序回放以保证最终一致性。
            // - MirrorSync.SyncChanges 返回从镜像上次版本到主列表当前的增量记录，可用于增量应用。

            // 1) 初始化
            versionedList.SetAutoApplyBuffers(false); // 关闭自动提交，演示手动申请缓冲
            mirrorSyncList.Source = versionedList;

            // 2) 主列表产生一系列操作（含记录/不记录）
            versionedList.Add(1);                // 记录并入队 (等同 Add(1, true))
            versionedList.Add(2);                // 记录
            versionedList.Remove(1, false);     // 入队但不写入版本记录（示例：镜像端忽略版本）
            versionedList.AddRange(new List<int> { 3, 4 });

            // 3) 提交缓冲（将按入队顺序回放到 ValuesNow）
            versionedList.ApplyBuffers(); // 或者 ApplyBuffersIfDirty()

            // 4) 更新版本索引（通知镜像可以拉取新版本）
            versionedList.UpdateVersion();

            // 5) 镜像端：拉取并应用增量（示例：一个简单的本地 mirrorList）
            List<int> mirrorList = new List<int>();
            mirrorSyncList.UpdateVersionToMaxOnly(); // 将镜像版本跳到最新（平滑起点）
            foreach (var change in mirrorSyncList.SyncChanges())
            {
                if (change.IsAdd) mirrorList.Add(change.value);
                else mirrorList.Remove(change.value);
            }

            // 6) 若镜像需要在本地改动但不希望写入主版本，可使用镜像 API
            mirrorSyncList.Add_IgnoreVersion(99); // 在主列表入队但标记不作为版本变更（内部会调用 Add(..., false)）

            // 7) 版本修剪与历史访问
            //    - 通过 versionedList.VersionedRecordChanges 与 VersionMin/VersionMax 可以读取历史增量区间
            //    - 调用 UpdateVersion 会根据已绑定的 MirrorSyncListValidator 自动修剪已被所有镜像消费的旧记录
            var records = versionedList.VersionedRecordChanges;
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                // 处理历史记录（例如持久化或调试输出）
            }

            

            // 性能与 GC 注意事项：
            // - 已重构为环形缓冲（opBuffer）以尽量避免在高频入队/回放时产生临时分配。
            // - Get/Contains 操作通过按序回放操作判断最终状态，避免额外集合分配，但对大型操作流会有遍历成本。
            // - 若操作量极大且需要批量合并，考虑周期性调用 ApplyBuffers 并在合并后清理历史记录以节省内存。

            // 小结：
            // - 主列表用 Add/Remove(+versionRecord) 写入并记录，镜像通过 SyncChanges 增量拉取并应用。
            // - 使用 Add_IgnoreVersion/Remove_IgnoreVersion 在镜像端进行本地变更时避免污染版本记录。
            // - UpdateVersion 会根据绑定的镜像状态尝试修剪历史记录，减少版本存储压力。
        }
        

          #region 典型使用场景

        // 典型场景：服务端为权威主列表，客户端持有镜像并周期性拉取增量应用。
        // 场景特点：高频写入在服务端集中处理，客户端尽量只拉增量并本地缓存展示。
        void MethodsForVersionedMirrorTypical()
        {
            // 假设：本 MonoBehaviour 在服务端环境运行，versionedList 为服务器侧数据源
            // 1) 服务端写入（记录版本）
            versionedList.Add(100);             // 记录并入队
            versionedList.Add(101);
            versionedList.Remove(50);
            versionedList.ApplyBuffers();       // 按序提交到 ValuesNow
            versionedList.UpdateVersion();      // 增量记录可被镜像拉取

            // 2) 客户端镜像初始化并拉取（伪代码示例，客户端可在自己的上下文中运行）
            var clientMirror = new MirrorSync<int>();
            clientMirror.Source = versionedList; // 绑定服务端源（通常通过网络代理或 RPC 实现）
            clientMirror.UpdateVersionToMaxOnly();
            var clientData = new List<int>();
            foreach (var change in clientMirror.SyncChanges())
            {
                if (change.IsAdd) clientData.Add(change.value);
                else clientData.Remove(change.value);
            }

            // 3) 客户端本地临时修改（不应写入版本）
            //    客户端希望本地展示或预测，但不想影响主版本历史，使用忽略版本的方法
            mirrorSyncList.Source = versionedList; // 在同一进程下示意绑定
            mirrorSyncList.Add_IgnoreVersion(200); // 主列表入队但不计版本

            // 4) 冲突策略（示例说明）：
            //    - 若客户端本地修改冲突于后续服务端版本，以服务端为准，客户端在下一次 SyncChanges 后将被覆盖或回退。
            //    - 若客户端需要最终一致且不允许覆盖，应把本地修改发送到服务端，由服务端决定是否 Accept，并写入版本。

            // 5) 版本修剪：绑定镜像的 Validator 可让服务端在所有镜像消费后修剪历史，减少内存占用
            var validator = new MirrorSyncListValidator<int>(clientMirror, () => true);
            versionedList.BindMirrorValidators(validator);

            // 小结：该典型场景强调“服务端权威、客户端增量拉取、本地忽略版本用于预测/展示、通过 Validator 控制历史修剪”。
        }

        #endregion
        #endregion
 
    }
}
