using System;
using UnityEngine;

namespace ES
{

    public static partial class EnumCollect
    {
        // <Buff的增益辨识>
        public enum BuffTagForGoodOrBad
        {
            [InspectorName("增益")]Good,
            [InspectorName("减益")] Bad,
            [InspectorName("不清楚")] NotClear
        }
        //技能点-处于的状态
        public enum SkillPointOneLevelState
        {
            [InspectorName("无_不显示")] None,
            [InspectorName("未知详情-显示为\"?\"")] UnknownDetail,
            [InspectorName("不允许解锁")] CantUnlock,
            [InspectorName("可解锁但条件不充分")] CanUnlockButOptionNotFeet,
            [InspectorName("条件完全达成")] CanUnlockComplete,
            [InspectorName("已解锁")] Unlock
        }

        //碰撞发生时  对XX编辑器 对 发起者，筛选者, 碰撞者 的相对关系对调
        public enum HandleWhenCol_OnWhichEntityOption
        {
            [InspectorName("作用在选择者,被发起者")] OnSelectorByLauncher,
            [InspectorName("作用在选择者,被碰撞者")] OnSelectorByCol,
            [InspectorName("作用在被碰撞者,被发起者")] OnColByLaucher,
            [InspectorName("作用在被碰撞者,被选择者")] OnColBySelector,
            [InspectorName("作用在发起者,被选择者")] OnLauncherBySelector,
            [InspectorName("作用在发起者,被碰撞者")] OnLauncherByCol
        }

        //飞行物--设置目标执行的操作
        public enum SetFlyingTargetAboutDirecOption
        {
            [InspectorName("不改变方向")] None,
            [InspectorName("直接面向")] Directly,
            [InspectorName("抛物线")] Parabola,
            [InspectorName("弧形")] RadAndFollow,
            [InspectorName("按默认设置")] BySelfDefault
        }

        //设置飞行物的运动方式基准
        public enum SetFlyingBaseOn
        {
            [InspectorName("刚体固定更新")] RigidFixUpdate,
            [InspectorName("刚体设置一次速度")] RigidVelocityOnce,
            [InspectorName("普通变换更新")] TransUpdate,
            [InspectorName("刚体速度渐进")] RigidVelocityUpdating,
        }

        //设置飞行物销毁原因
        [Flags]
        public enum SetFlyingDestroyWhyOption
        {
            [InspectorName("常用")]
            Normal = OnTriEntity | LifeTime,
            [InspectorName("生命周期到期")] LifeTime = 1,
            [InspectorName("触碰实体")] OnTriEntity = 2,
            [InspectorName("被环境阻挡")] OnBeBlockEnviroment = 4,
            [InspectorName("被实体格挡")] OnBeBlockByEntity = 8,
            [InspectorName("被飞行物碰到")] OnBeBlockByFlying = 16,
            [InspectorName("加载销毁")] OnLoadKill = 32,
        }

        //实体游荡目的地选择
        public enum EntityMotionTargetPosSelect
        {
            [InspectorName("按预备数字计数")] Numerically = 0,
            [InspectorName("随机")] Random = 1,
            [InspectorName("程序化生成")] ProcedurallyWaypoints = 2,
            [InspectorName("玩家作为坐标")] PlayerTarget = 3
        }

        //实体(或者任意攻击)的基准攻击形式
        [Flags]
        public enum EntityAttackBaseType 
        {
            [InspectorName("未定义")]None=0,
            [InspectorName("近战成分")] Melle = 1,
            [InspectorName("远程成分")] Range = 2,
            [InspectorName("同时具备近战远程")] MelleAndRange = Range | Melle,
        }

        //实体攻击触发(造成伤害)成功 条件
        [Flags]
        public enum EntityAttackOnHappenCondition
        {
            [InspectorName("直接命中")] Direct = 1,
            [InspectorName("按动作和武器(HitBox)")] AsAction = 2,
            [InspectorName("执行序列")] HandleSequence = 4
        }

        //实体开始攻击的 条件
        public enum EntityTryStartAttackCondition
        {
            [InspectorName("必须目标在范围内")] MustTargetInRange,
            [InspectorName("必须有目标即可")] MustTargetAlive,
            [InspectorName("无条件攻击")] Anyway,
        }

        
    }
}

