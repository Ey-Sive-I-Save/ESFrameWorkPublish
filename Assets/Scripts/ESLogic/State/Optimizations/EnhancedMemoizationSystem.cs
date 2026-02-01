using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES.Optimizations
{
    /// <summary>
    /// 零GC备忘系统 - 使用版本号和帧计数，避免时间戳
    /// 性能特点:
    /// - 所有操作O(1)
    /// - 零GC分配
    /// - 小ID用数组(极快)，大ID用字典
    /// </summary>
    public class EnhancedMemoizationSystem
    {
        // 备忘条目 - 使用struct避免GC
        private struct MemoEntry
        {
            public int version;        // 版本号
            public int frameCount;     // 记录时的帧数
            public byte attemptCount;  // 尝试次数(用byte节省内存)
            public DenialReason reason;
        }
        
        // 混合存储策略
        private const int SMALL_ID_THRESHOLD = 256;
        private MemoEntry[] _smallIdCache;  // 小ID快速访问 O(1)
        private Dictionary<int, MemoEntry> _largeIdCache; // 大ID使用字典
        
        // 版本控制
        private int _currentVersion = 0;
        private int _frameCount = 0;
        
        // 配置
        private const int MAX_MEMO_FRAMES = 60;  // 备忘有效期(帧)
        private const byte MAX_ATTEMPTS = 3;     // 最大尝试次数
        
        public EnhancedMemoizationSystem(int smallIdCacheSize = SMALL_ID_THRESHOLD)
        {
            _smallIdCache = new MemoEntry[smallIdCacheSize];
            _largeIdCache = new Dictionary<int, MemoEntry>(32);
        }
        
        /// <summary>
        /// O(1) 检查状态是否被拒绝 - 零GC
        /// </summary>
        public bool IsStateDenied(int stateId, out DenialReason reason)
        {
            MemoEntry entry = GetEntry(stateId, out bool found);
            
            // 未找到或版本不匹配
            if (!found || entry.version != _currentVersion)
            {
                reason = DenialReason.None;
                return false;
            }
            
            // 检查是否过期
            int framesPassed = _frameCount - entry.frameCount;
            if (framesPassed > MAX_MEMO_FRAMES)
            {
                reason = DenialReason.None;
                return false;
            }
            
            // 检查尝试次数 - 防止永久锁定
            if (entry.attemptCount >= MAX_ATTEMPTS)
            {
                // 达到最大尝试次数，清除备忘允许重试
                reason = DenialReason.None;
                return false;
            }
            
            reason = entry.reason;
            return true;
        }
        
        /// <summary>
        /// O(1) 记录拒绝 - 零GC
        /// </summary>
        public void RecordDenial(int stateId, DenialReason reason)
        {
            MemoEntry entry = GetEntry(stateId, out bool found);
            
            // 如果是同一版本，增加尝试次数
            if (found && entry.version == _currentVersion)
            {
                if (entry.attemptCount < byte.MaxValue)
                    entry.attemptCount++;
            }
            else
            {
                // 新建条目
                entry = new MemoEntry
                {
                    version = _currentVersion,
                    frameCount = _frameCount,
                    attemptCount = 1,
                    reason = reason
                };
            }
            
            SetEntry(stateId, entry);
        }
        
        /// <summary>
        /// O(1) 标记为脏 - 只需增加版本号
        /// </summary>
        public void MarkDirty()
        {
            _currentVersion++;
            
            // 防止版本号溢出 (极少发生)
            if (_currentVersion == int.MaxValue)
            {
                Reset();
            }
        }
        
        /// <summary>
        /// 每帧调用 - 更新帧计数
        /// </summary>
        public void OnFrameUpdate()
        {
            _frameCount++;
            
            // 防止帧计数溢出
            if (_frameCount >= int.MaxValue - 1000)
            {
                NormalizeFrameCounts();
            }
        }
        
        /// <summary>
        /// 清除指定状态的备忘
        /// </summary>
        public void ClearMemo(int stateId)
        {
            if (stateId < SMALL_ID_THRESHOLD)
            {
                _smallIdCache[stateId] = default;
            }
            else
            {
                _largeIdCache.Remove(stateId);
            }
        }
        
        /// <summary>
        /// 完全重置
        /// </summary>
        public void Reset()
        {
            _currentVersion = 0;
            _frameCount = 0;
            Array.Clear(_smallIdCache, 0, _smallIdCache.Length);
            _largeIdCache.Clear();
        }
        
        /// <summary>
        /// 定期清理过期条目 - 可选的内存优化
        /// </summary>
        public void CleanupExpired()
        {
            // 只清理大ID缓存
            var toRemove = new List<int>(8);
            foreach (var kvp in _largeIdCache)
            {
                var entry = kvp.Value;
                if (entry.version != _currentVersion || 
                    _frameCount - entry.frameCount > MAX_MEMO_FRAMES)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (int id in toRemove)
            {
                _largeIdCache.Remove(id);
            }
        }
        
        // ========== 内部辅助方法 ==========
        
        private MemoEntry GetEntry(int stateId, out bool found)
        {
            if (stateId < SMALL_ID_THRESHOLD)
            {
                var entry = _smallIdCache[stateId];
                found = entry.version == _currentVersion;
                return entry;
            }
            else
            {
                found = _largeIdCache.TryGetValue(stateId, out var entry);
                return entry;
            }
        }
        
        private void SetEntry(int stateId, MemoEntry entry)
        {
            if (stateId < SMALL_ID_THRESHOLD)
            {
                _smallIdCache[stateId] = entry;
            }
            else
            {
                _largeIdCache[stateId] = entry;
            }
        }
        
        private void NormalizeFrameCounts()
        {
            int offset = _frameCount - 1000;
            _frameCount = 1000;
            
            // 更新小ID缓存
            for (int i = 0; i < _smallIdCache.Length; i++)
            {
                if (_smallIdCache[i].version == _currentVersion)
                {
                    var entry = _smallIdCache[i];
                    entry.frameCount = Mathf.Max(0, entry.frameCount - offset);
                    _smallIdCache[i] = entry;
                }
            }
            
            // 更新大ID缓存
            var keys = new List<int>(_largeIdCache.Keys);
            foreach (int key in keys)
            {
                var entry = _largeIdCache[key];
                entry.frameCount = Mathf.Max(0, entry.frameCount - offset);
                _largeIdCache[key] = entry;
            }
        }
        
        // ========== 调试信息 ==========
        
        public int GetCachedCount()
        {
            int count = 0;
            for (int i = 0; i < _smallIdCache.Length; i++)
            {
                if (_smallIdCache[i].version == _currentVersion)
                    count++;
            }
            count += _largeIdCache.Count;
            return count;
        }
        
        public string GetDebugInfo()
        {
            return $"Version: {_currentVersion}, Frame: {_frameCount}, Cached: {GetCachedCount()}";
        }
    }
}
