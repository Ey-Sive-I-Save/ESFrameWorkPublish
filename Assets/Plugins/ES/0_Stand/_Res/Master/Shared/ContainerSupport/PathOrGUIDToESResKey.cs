using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace ES
{
      /// <summary>
    /// 路径或GUID到ESResKey的映射：用于资源加载系统，通过路径或GUID查询ESResKey
    /// </summary>
    public class PathOrGUIDToESResKey : TwoStringKeyDictionary<ESResKey>
    {
        /// <summary>
        /// 通过路径获取ESResKey
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <returns>ESResKey</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESResKey GetESResKeyByPath(string path)
        {
            return GetByKey1(path);
        }

        /// <summary>
        /// 通过GUID获取ESResKey
        /// </summary>
        /// <param name="guid">资源GUID</param>
        /// <returns>ESResKey</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESResKey GetESResKeyByGUID(string guid)
        {
            return GetByKey2(guid);
        }

        /// <summary>
        /// 尝试通过路径获取ESResKey
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="resKey">输出的ESResKey</param>
        /// <returns>是否找到</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetESResKeyByPath(string path, out ESResKey resKey)
        {
            return TryGetByKey1(path, out resKey);
        }

        /// <summary>
        /// 尝试通过GUID获取ESResKey
        /// </summary>
        /// <param name="guid">资源GUID</param>
        /// <param name="resKey">输出的ESResKey</param>
        /// <returns>是否找到</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetESResKeyByGUID(string guid, out ESResKey resKey)
        {
            return TryGetByKey2(guid, out resKey);
        }
    }

}
