using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //匹配器
        public static class Matcher
        {
            public static string EnumToString_SkillPointState(EnumCollect.SkillPointOneLevelState state)
            {
                switch (state)
                {
                    case EnumCollect.SkillPointOneLevelState.None: return "无_不显示";
                    case EnumCollect.SkillPointOneLevelState.UnknownDetail: return "未知详情-显示为?";
                    case EnumCollect.SkillPointOneLevelState.CantUnlock: return "不允许解锁";
                    case EnumCollect.SkillPointOneLevelState.CanUnlockButOptionNotFeet: return "可解锁但条件未达到";
                    case EnumCollect.SkillPointOneLevelState.CanUnlockComplete: return "条件完全达成";
                    case EnumCollect.SkillPointOneLevelState.Unlock: return "解锁";
                }
                return "空定义";
            }

           
            public static T SystemObjectToT<T>(object from)
            {
                Type type = typeof(T);
                return (T)SystemObjectToT(from, type);
            }
            public static object SystemObjectToT(object from, Type type)
            {
                if (type == typeof(float))
                {
                    return Convert.ChangeType(Convert.ToSingle(from), typeof(float));
                }
                else
                {
                    return Convert.ChangeType(from, type);
                }

            }
        }
    }
}

