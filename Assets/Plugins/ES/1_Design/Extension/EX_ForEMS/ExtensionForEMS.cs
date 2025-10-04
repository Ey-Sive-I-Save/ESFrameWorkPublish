using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public static class ExtensionForEMS
    {
        #region 脚本与游戏对象万能的
        public static EMS _GetOrAddAnyEMS<EMS>(this Component com) where EMS : EMS_Abstract
        {
            var ems = com.GetComponent<EMS>();
            if (ems != null) return ems;
            return com.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMS<EMS>(this GameObject ga) where EMS : EMS_Abstract
        {
            var ems = ga.GetComponent<EMS>();
            if (ems != null) return ems;
            return ga.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInParent<EMS>(this Component com) where EMS : EMS_Abstract
        {
            var ems = com.GetComponentInParent<EMS>();
            if (ems != null) return ems;
            return com.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInParent<EMS>(this GameObject ga) where EMS : EMS_Abstract
        {
            var ems = ga.GetComponentInParent<EMS>();
            if (ems != null) return ems;
            return ga.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInChildren<EMS>(this Component com) where EMS : EMS_Abstract
        {
            var ems = com.GetComponentInChildren<EMS>();
            if (ems != null) return ems;
            return com.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInChildren<EMS>(this GameObject ga) where EMS : EMS_Abstract
        {
            var ems = ga.GetComponentInChildren<EMS>();
            if (ems != null) return ems;
            return ga.AddComponent<EMS>();
        }
        #endregion

        #region 域和剪影支持
        public static EMS _GetOrAddAnyEMS<EMS>(this IDomain domain) where EMS : EMS_Abstract
        {
            var ems = domain.Core_Base.GetComponent<EMS>();
            if (ems != null) return ems;
            return domain.Core_Base.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMS<EMS>(this IModule mo) where EMS : EMS_Abstract
        {
            var ems = mo.Core_Object.GetComponent<EMS>();
            if (ems != null) return ems;
            return mo.Core_Object.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInParent<EMS>(this IDomain domain) where EMS : EMS_Abstract
        {
            var ems = domain.Core_Base.GetComponentInParent<EMS>();
            if (ems != null) return ems;
            return domain.Core_Base.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInParent<EMS>(this IModule mo) where EMS : EMS_Abstract
        {
            var ems = mo.Core_Object.GetComponentInParent<EMS>();
            if (ems != null) return ems;
            return mo.Core_Object.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInChildren<EMS>(this IDomain domain) where EMS : EMS_Abstract
        {
            var ems = domain.Core_Base.GetComponentInChildren<EMS>();
            if (ems != null) return ems;
            return domain.Core_Base.gameObject.AddComponent<EMS>();
        }
        public static EMS _GetOrAddAnyEMSInChildren<EMS>(this IModule mo) where EMS : EMS_Abstract
        {
            var ems = mo.Core_Object.GetComponentInChildren<EMS>();
            if (ems != null) return ems;
            return mo.Core_Object.gameObject.AddComponent<EMS>();
        } 

        #endregion


        
    }
}
