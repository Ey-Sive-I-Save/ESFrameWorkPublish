namespace ES
{
    /// <summary>
    /// 主攻击的执行类别。Action 可承载徒手、近战武器、双持组合技等作者定义攻击；
    /// 非 Action 值直接表达 WeaponDefinition 选择的交付后端。
    /// </summary>
    public enum EntityPrimaryAttackRoute
    {
        None = 0,
        Action = 1,
        HitScan = 2,
        Shot = 3,
        Beam = 4,
    }

    /// <summary>
    /// 本次普攻由什么来源产生，供下游按自身规则识别攻击上下文。
    /// 来源不代表输入链、物理后端、结算方式或任何具体玩法效果。
    /// </summary>
    public enum EntityPrimaryAttackSource
    {
        None = 0,
        Unarmed = 1,
        PrimaryWeapon = 2,
        SecondaryWeapon = 3,
        PairedWeapons = 4,
    }

    public readonly struct EntityPrimaryAttackSelection
    {
        public readonly EntityPrimaryAttackRoute route;
        public readonly EntityPrimaryAttackSource source;

        public EntityPrimaryAttackSelection(
            EntityPrimaryAttackRoute route,
            EntityPrimaryAttackSource source)
        {
            this.route = route;
            this.source = source;
        }

        public bool IsValid => route != EntityPrimaryAttackRoute.None
                               && source != EntityPrimaryAttackSource.None;

        public static EntityPrimaryAttackSelection None => new EntityPrimaryAttackSelection(
            EntityPrimaryAttackRoute.None,
            EntityPrimaryAttackSource.None);
    }

    /// <summary>
    /// 只依据当前武器定义选择主攻击类别，不注册任务、不排序，也不执行 Action、Transform 或物理副作用。
    /// weaponKind 只做内容分类，不能参与执行路由。
    /// </summary>
    public static class EntityPrimaryAttackSelector
    {
        public static EntityPrimaryAttackSelection Select(
            ItemWeaponSharedData definition,
            ESActionConfigKey actionKey,
            EntityPrimaryAttackSource source = EntityPrimaryAttackSource.PrimaryWeapon)
        {
            if (definition == null)
                return EntityPrimaryAttackSelection.None;

            switch (definition.deliveryMode)
            {
                case WeaponAttackDeliveryMode.Action:
                    return SelectAction(actionKey, source);
                case WeaponAttackDeliveryMode.HitScan:
                    return SelectDelivery(EntityPrimaryAttackRoute.HitScan, definition, source);
                case WeaponAttackDeliveryMode.Shot:
                    return definition.defaultShot != null && definition.defaultShot.IsConfigured
                        ? SelectDelivery(EntityPrimaryAttackRoute.Shot, definition, source)
                        : EntityPrimaryAttackSelection.None;
                case WeaponAttackDeliveryMode.Beam:
                    return SelectDelivery(EntityPrimaryAttackRoute.Beam, definition, source);
                default:
                    return EntityPrimaryAttackSelection.None;
            }
        }

        public static EntityPrimaryAttackSelection SelectUnarmed(ESActionConfigKey actionKey)
        {
            return SelectAction(actionKey, EntityPrimaryAttackSource.Unarmed);
        }

        /// <summary>
        /// 双持普攻必须由明确的成对 Action 定义表达。不能把两把武器各执行一次、
        /// 或在第一把失败后回退第二把，避免把一次攻击错误拆成两套生命周期。
        /// </summary>
        public static EntityPrimaryAttackSelection SelectPairedWeapons(ESActionConfigKey pairedActionKey)
        {
            return SelectAction(pairedActionKey, EntityPrimaryAttackSource.PairedWeapons);
        }

        private static EntityPrimaryAttackSelection SelectAction(
            ESActionConfigKey actionKey,
            EntityPrimaryAttackSource source)
        {
            return actionKey != null
                   && actionKey.IsConfigured
                   && source != EntityPrimaryAttackSource.None
                ? new EntityPrimaryAttackSelection(EntityPrimaryAttackRoute.Action, source)
                : EntityPrimaryAttackSelection.None;
        }

        private static EntityPrimaryAttackSelection SelectDelivery(
            EntityPrimaryAttackRoute route,
            ItemWeaponSharedData definition,
            EntityPrimaryAttackSource source)
        {
            return definition.fire != null
                   && definition.fire.enabled
                   && source != EntityPrimaryAttackSource.None
                ? new EntityPrimaryAttackSelection(route, source)
                : EntityPrimaryAttackSelection.None;
        }
    }
}
