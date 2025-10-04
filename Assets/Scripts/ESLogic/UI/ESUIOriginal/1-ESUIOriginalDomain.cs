using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [Serializable, TypeRegistryItem("原始UI扩展域")]
    public class ESUIOriginalDomain : Domain<ESUIElement, ESUIOriginalModule>
    {

    }
    [Serializable, TypeRegistryItem("原始UI模块")]
    public abstract class ESUIOriginalModule : Module<ESUIElement, ESUIOriginalDomain>
    {

    }
}
