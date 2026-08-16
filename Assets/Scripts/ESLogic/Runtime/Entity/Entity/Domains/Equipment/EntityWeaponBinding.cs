using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Weapon-local presentation references. Character attachment sockets are never stored here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityWeaponBinding : MonoBehaviour
    {
        [Title("武器局部参考点")]
        [SerializeField, LabelText("主手握点")]
        private Transform gripPivot;

        [SerializeField, LabelText("副手握点")]
        private Transform offHandGrip;

        [SerializeField, LabelText("枪口")]
        private Transform muzzle;

        [SerializeField, LabelText("瞄准参考")]
        private Transform aimReference;

        [SerializeField, LabelText("表现根")]
        private GameObject presentationRoot;

        [Title("武器语义")]
        [LabelText("双手握持")]
        public bool twoHanded;

        [Title("状态键覆盖")]
        [LabelText("拿枪状态键")]
        public string equipStateKey;

        [LabelText("拿枪状态 AniInfo")]
        public StateAniDataInfo equipStateInfo;

        [LabelText("收枪状态键")]
        public string holsterStateKey;

        [LabelText("收枪状态 AniInfo")]
        public StateAniDataInfo holsterStateInfo;

        [LabelText("切枪状态键")]
        public string switchStateKey;

        [LabelText("切枪状态 AniInfo")]
        public StateAniDataInfo switchStateInfo;

        [LabelText("开火状态键")]
        public string fireStateKey;

        [LabelText("开火状态 AniInfo")]
        public StateAniDataInfo fireStateInfo;

        [Title("Tag")]
        [LabelText("手持时添加")]
        public List<ESTagStableReference> equippedTags = new List<ESTagStableReference>();

        [Title("调试")]
        [ReadOnly, ShowInInspector, LabelText("武器名")]
        public string WeaponName => gameObject.name;

        public Transform GripPivot => gripPivot;
        public Transform OffHandGrip => offHandGrip;
        public Transform Muzzle => muzzle;
        public Transform AimReference => aimReference;
        public GameObject PresentationRoot => presentationRoot;
        public bool IsPresentationVisible => PresentationRoot.activeSelf;

        public void ConfigureReferences(
            Transform newGripPivot,
            Transform newOffHandGrip,
            Transform newMuzzle,
            Transform newAimReference,
            GameObject newPresentationRoot)
        {
            gripPivot = newGripPivot;
            offHandGrip = newOffHandGrip;
            muzzle = newMuzzle;
            aimReference = newAimReference;
            presentationRoot = newPresentationRoot;
        }

        public bool ValidateReferences(out string error)
        {
            if (!IsOwned(gripPivot))
            {
                error = "GripPivot must belong to the weapon root.";
                return false;
            }
            if (twoHanded && !IsOwned(offHandGrip))
            {
                error = "A two-handed weapon requires an owned OffHandGrip.";
                return false;
            }
            if (muzzle != null && !IsOwned(muzzle))
            {
                error = "Muzzle must belong to the weapon root.";
                return false;
            }
            if (aimReference != null && !IsOwned(aimReference))
            {
                error = "AimReference must belong to the weapon root.";
                return false;
            }
            if (presentationRoot == null)
            {
                error = "PresentationRoot must be authored on the weapon root.";
                return false;
            }
            if (presentationRoot.transform != transform
                && !presentationRoot.transform.IsChildOf(transform))
            {
                error = "PresentationRoot must belong to the weapon root.";
                return false;
            }

            error = null;
            return true;
        }

        public void SetPresentationVisible(bool visible)
        {
            GameObject target = PresentationRoot;
            if (target.activeSelf != visible)
                target.SetActive(visible);
        }

        private bool IsOwned(Transform target)
        {
            return target != null && (target == transform || target.IsChildOf(transform));
        }
    }
}
