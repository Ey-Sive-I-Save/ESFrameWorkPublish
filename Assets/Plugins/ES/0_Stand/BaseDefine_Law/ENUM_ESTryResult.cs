using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [Flags]
    public enum ESTryResult : byte
    {
        /// <summary>
        /// 失败
        /// </summary>
        [InspectorName("失败")] Fail = 1,
        /// <summary>
        /// 成功
        /// </summary>
        [InspectorName("成功")] Succeed = 2,
        /// <summary>
        /// 无意义重试
        /// </summary>
        [InspectorName("无意义重试")] ReTry = 4,
        /// <summary>
        /// 还没结果
        /// </summary>
        [InspectorName("还没结果")] Trying = 8
    }


}
