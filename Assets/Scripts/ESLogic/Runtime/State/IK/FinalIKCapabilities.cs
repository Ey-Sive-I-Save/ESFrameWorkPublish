using System;
using UnityEngine;
using RootMotion.FinalIK;

// ============================================================================
// 文件：FinalIKCapabilities.cs
// 作用：在 StateMachine.BindToAnimator 时 **一次性** 扫描 Animator 同物体上挂载的
//       FinalIK 组件，生成 FinalIKCapabilityFlags 枚举并缓存各组件引用。
//       此后所有 IK 路径只检查 bool/flag，不再反复做 GetComponent/null check。
//
// 设计原则：
//   - Bind() 一次扫描，结果存入 FinalIKCapabilities 值类型。
//   - 组件引用放在 ref 类 FinalIKComponentRefs（引用类型，避免装箱）。
//   - FinalIKCapabilityFlags 用 [Flags] 枚举，支持 HasFlag 位运算快速判断。
//
// FinalIK 插件全量可用功能（当前工程已安装的）：
// ┌──────────────────────┬───────────────────────────────────────────────────┐
// │ 组件                 │ 用途                                              │
// ├──────────────────────┼───────────────────────────────────────────────────┤
// │ BipedIK              │ 双足四肢 IK + LookAt（当前 ES 框架主要使用）     │
// │ FullBodyBipedIK      │ 全身 IK + 脊椎弯曲 + 效应器偏移系统              │
// │ AimIK                │ 骨链瞄准目标（武器/身体对准某点）                 │
// │ LookAtIK             │ 多骨骼 LookAt（比 BipedIK 内置 LookAt 更精细）   │
// │ GrounderBipedIK      │ BipedIK 地形自适应脚步接地                        │
// │ GrounderFBBIK        │ FullBodyBipedIK 脚步接地                          │
// │ HitReaction          │ FBBIK 受击程序动画（冲击波形驱动效应器偏移）      │
// │ Recoil               │ FBBIK 武器后坐力程序动画                          │
// └──────────────────────┴───────────────────────────────────────────────────┘
// ============================================================================

namespace ES
{
    /// <summary>
    /// FinalIK 功能标志位。Bind 时一次检测，之后所有路径按此 flags 分支，零反射零 GC。
    /// </summary>
    [Flags]
    public enum FinalIKCapabilityFlags
    {
        None              = 0,

        /// <summary>BipedIK：双足四肢 IK + LookAt。ES IK 系统当前主驱动。</summary>
        BipedIK           = 1 << 0,

        /// <summary>FullBodyBipedIK：全身 IK + 脊椎弯曲 + 效应器偏移。可替换 BipedIK 作为更强力的选择。</summary>
        FullBodyBipedIK   = 1 << 1,

        /// <summary>AimIK：骨链瞄准（武器持械 / 身体转向目标点）。</summary>
        AimIK             = 1 << 2,

        /// <summary>LookAtIK：多骨骼注视（头/颈/脊椎层级 LookAt，比 BipedIK 内置更精细）。</summary>
        LookAtIK          = 1 << 3,

        /// <summary>GrounderBipedIK：配合 BipedIK 使用的地形脚步接地系统。</summary>
        GrounderBipedIK   = 1 << 4,

        /// <summary>GrounderFBBIK：配合 FullBodyBipedIK 使用的地形脚步接地系统。</summary>
        GrounderFBBIK     = 1 << 5,

        /// <summary>HitReaction：基于 FBBIK 的受击程序动画（需要 FullBodyBipedIK）。</summary>
        HitReaction       = 1 << 6,

        /// <summary>Recoil：基于 FBBIK 的武器后坐力程序动画（需要 FullBodyBipedIK）。</summary>
        Recoil            = 1 << 7,
    }

    /// <summary>
    /// 在 Bind 时一次性填充的 FinalIK 组件引用（引用类型，不装箱、零 GC 访问）。
    /// 非 null 的字段与 FinalIKCapabilityFlags 对应位保持一致。
    /// </summary>
    
    public sealed class FinalIKComponentRefs
    {
        // ── 主 IK 求解器 ──────────────────────────────────────────────────────
        public BipedIK          bipedIK;           // flag: BipedIK
        public FullBodyBipedIK  fullBodyBipedIK;   // flag: FullBodyBipedIK
        public AimIK            aimIK;             // flag: AimIK
        public LookAtIK         lookAtIK;          // flag: LookAtIK

        // ── 接地系统 ──────────────────────────────────────────────────────────
        public GrounderBipedIK  grounderBipedIK;   // flag: GrounderBipedIK
        public GrounderFBBIK    grounderFBBIK;     // flag: GrounderFBBIK

        // ── 程序动画工具 ─────────────────────────────────────────────────────
        public HitReaction      hitReaction;       // flag: HitReaction
        public Recoil           recoil;            // flag: Recoil

        /// <summary>
        /// 按需扫描 Animator 同物体上的 FinalIK 组件，只查询 <paramref name="want"/> 中包含的功能。
        /// 只在 Bind 时调用一次；禁用的功能不产生任何 GetComponent 开销。
        /// </summary>
        /// <param name="animator">目标 Animator。</param>
        /// <param name="want">需要扫描的功能集合（由各 enable*** 字段合成传入）。</param>
        public FinalIKCapabilityFlags Scan(Animator animator, FinalIKCapabilityFlags want)
        {
            if (animator == null) return FinalIKCapabilityFlags.None;

            var go    = animator.gameObject;
            var flags = FinalIKCapabilityFlags.None;

            // 每个分支只在对应功能被启用时才执行 GetComponent，禁用功能零查询开销。
            if ((want & FinalIKCapabilityFlags.BipedIK)         != 0)
            { bipedIK         = go.GetComponent<BipedIK>();         if (bipedIK         != null) flags |= FinalIKCapabilityFlags.BipedIK;         }
            if ((want & FinalIKCapabilityFlags.FullBodyBipedIK) != 0)
            { fullBodyBipedIK = go.GetComponent<FullBodyBipedIK>(); if (fullBodyBipedIK != null) flags |= FinalIKCapabilityFlags.FullBodyBipedIK; }
            if ((want & FinalIKCapabilityFlags.AimIK)           != 0)
            { aimIK           = go.GetComponent<AimIK>();           if (aimIK           != null) flags |= FinalIKCapabilityFlags.AimIK;           }
            if ((want & FinalIKCapabilityFlags.LookAtIK)        != 0)
            { lookAtIK        = go.GetComponent<LookAtIK>();        if (lookAtIK        != null) flags |= FinalIKCapabilityFlags.LookAtIK;        }
            if ((want & FinalIKCapabilityFlags.GrounderBipedIK) != 0)
            { grounderBipedIK = go.GetComponent<GrounderBipedIK>(); if (grounderBipedIK != null) flags |= FinalIKCapabilityFlags.GrounderBipedIK; }
            if ((want & FinalIKCapabilityFlags.GrounderFBBIK)   != 0)
            { grounderFBBIK   = go.GetComponent<GrounderFBBIK>();   if (grounderFBBIK   != null) flags |= FinalIKCapabilityFlags.GrounderFBBIK;   }
            if ((want & FinalIKCapabilityFlags.HitReaction)     != 0)
            { hitReaction     = go.GetComponent<HitReaction>();     if (hitReaction     != null) flags |= FinalIKCapabilityFlags.HitReaction;     }
            if ((want & FinalIKCapabilityFlags.Recoil)          != 0)
            { recoil          = go.GetComponent<Recoil>();          if (recoil          != null) flags |= FinalIKCapabilityFlags.Recoil;          }

            return flags;
        }

        /// <summary>Bind 解除时清空所有引用，避免持有已销毁 GameObject 引用。</summary>
        public void Clear()
        {
            bipedIK         = null;
            fullBodyBipedIK = null;
            aimIK           = null;
            lookAtIK        = null;
            grounderBipedIK = null;
            grounderFBBIK   = null;
            hitReaction     = null;
            recoil          = null;
        }
    }
}
