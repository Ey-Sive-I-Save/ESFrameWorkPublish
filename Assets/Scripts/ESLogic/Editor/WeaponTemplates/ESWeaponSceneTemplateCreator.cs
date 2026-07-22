using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    public static class ESWeaponSceneTemplateCreator
    {
        private const string MenuPath = "ES/武器/创建通用武器场景模板";

        [MenuItem(MenuPath, priority = 1600)]
        public static void CreateTemplate()
        {
            GameObject root = new GameObject("通用武器模板");
            Undo.RegisterCreatedObjectUndo(root, "Create Weapon Scene Template");

            var item = root.AddComponent<Item>();
            var template = root.AddComponent<ESWeaponSceneTemplate>();

            Transform runtimeRoot = CreateChild(root.transform, ESWeaponSceneTemplate.RuntimeRootName);
            Transform mountRoot = CreateChild(root.transform, ESWeaponSceneTemplate.MountRootName);
            Transform ballisticRoot = CreateChild(root.transform, ESWeaponSceneTemplate.BallisticRootName);
            Transform presentationRoot = CreateChild(root.transform, ESWeaponSceneTemplate.PresentationRootName);
            Transform debugRoot = CreateChild(root.transform, ESWeaponSceneTemplate.DebugRootName);

            Transform holdSocket = CreateChild(mountRoot, "HoldSocket", new Vector3(0f, 0f, 0f));
            Transform backSocket = CreateChild(mountRoot, "BackSocket", new Vector3(0f, 0.1f, -0.25f));
            Transform rightHandGrip = CreateChild(mountRoot, "RightHandGrip", new Vector3(0.03f, -0.03f, -0.08f));
            Transform leftHandGrip = CreateChild(mountRoot, "LeftHandGrip", new Vector3(-0.03f, -0.02f, 0.18f));
            Transform aimReference = CreateChild(mountRoot, "AimReference", new Vector3(0f, 0.05f, 0.32f));
            Transform recoilPivot = CreateChild(mountRoot, "RecoilPivot", new Vector3(0f, 0.02f, -0.05f));

            Transform muzzle = CreateChild(ballisticRoot, "Muzzle", new Vector3(0f, 0.03f, 0.55f));
            Transform shellEject = CreateChild(ballisticRoot, "ShellEject", new Vector3(0.09f, 0.03f, 0.05f));
            Transform magazine = CreateChild(ballisticRoot, "Magazine", new Vector3(0f, -0.12f, 0.02f));
            Transform chamber = CreateChild(ballisticRoot, "Chamber", new Vector3(0f, 0.02f, 0.08f));
            Transform rayOrigin = CreateChild(ballisticRoot, "RayOrigin", new Vector3(0f, 0.05f, 0.35f));
            Transform shotSpawn = CreateChild(ballisticRoot, "ShotSpawn", new Vector3(0f, 0.03f, 0.55f));

            Transform modelRoot = CreateChild(presentationRoot, "ModelRoot");
            Transform colliderRoot = CreateChild(presentationRoot, "ColliderRoot");
            Transform vfxRoot = CreateChild(presentationRoot, "VFXRoot");
            Transform audioRoot = CreateChild(presentationRoot, "AudioRoot");
            Transform animationRoot = CreateChild(presentationRoot, "AnimationRoot");

            CreateChild(vfxRoot, "MuzzleFlashPoint", new Vector3(0f, 0.03f, 0.55f));
            CreateChild(vfxRoot, "HitVFXPreviewPoint", new Vector3(0f, 0.03f, 0.9f));
            CreateChild(audioRoot, "FireAudioPoint", new Vector3(0f, 0.02f, 0.1f));
            CreateChild(debugRoot, "AimPreviewTarget", new Vector3(0f, 0.05f, 1.2f));

            template.runtimeBridge.itemRoot = item;
            template.mount.mountRoot = mountRoot;
            template.mount.holdSocket = holdSocket;
            template.mount.backSocket = backSocket;
            template.mount.rightHandGrip = rightHandGrip;
            template.mount.leftHandGrip = leftHandGrip;
            template.mount.aimReference = aimReference;
            template.mount.recoilPivot = recoilPivot;
            template.ballistic.ballisticRoot = ballisticRoot;
            template.ballistic.muzzle = muzzle;
            template.ballistic.shellEject = shellEject;
            template.ballistic.magazine = magazine;
            template.ballistic.chamber = chamber;
            template.ballistic.rayOrigin = rayOrigin;
            template.ballistic.shotSpawn = shotSpawn;
            template.presentation.presentationRoot = presentationRoot;
            template.presentation.modelRoot = modelRoot;
            template.presentation.colliderRoot = colliderRoot;
            template.presentation.vfxRoot = vfxRoot;
            template.presentation.audioRoot = audioRoot;
            template.presentation.animationRoot = animationRoot;
            template.debug.debugRoot = debugRoot;

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            return CreateChild(parent, name, Vector3.zero);
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create Weapon Scene Template Child");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }
    }
}
