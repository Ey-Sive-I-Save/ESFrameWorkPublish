using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Sources;
using UnityEditor;
using UnityEngine;


namespace ES
{
    [Serializable]
    public abstract class ESResReferAB
    {
        public abstract void Draw();
        
        
    }
    [Serializable]
    public class ESResRefer<T> : ESResReferAB where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        private bool Refresh = true;
        private UnityEngine.Object _obj;
#endif
        [OnInspectorGUI]
        public override void Draw()
        {
#if UNITY_EDITOR
            if (Refresh)
            {
                _obj = ESStandUtility.SafeEditor.LoadAssetByGUIDString(GUID);
                Refresh = false;
            }
            var c = EditorGUILayout.ObjectField(_obj, typeof(T), false);
            if (c != _obj)
            {
                Refresh = true;
                GUID = ESStandUtility.SafeEditor.GetAssetGUID(c);
            }
#endif
        }
        [SerializeField, HideInInspector]
        public string GUID = "";

        public virtual void TryLoadByLoaderASync(ESResLoader loader,Action<T> Use,bool atLastOrFirst=true)
        {
            loader.AddAsset2LoadByGUIDSourcer(GUID, (b, source) => { Debug.Log("加载哈"+b+"  "+source); if (b && source.Asset is T t) { Use.Invoke(t); } },atLastOrFirst);
            loader.LoadAllAsync();
        }

        public virtual bool TryLoadByLoaderSync(ESResLoader loader,out T tuse, bool atLastOrFirst = true)
        {
            bool back = false;
            T use = null;
            loader.AddAsset2LoadByGUIDSourcer(GUID, (b, source) => { if (b && source.Asset is T t) { back = true; use = t; } }, atLastOrFirst);
            loader.LoadAll_Sync();
            tuse = use;
            if (back)
            {
                return true;
            }
            return false;
        }
    }

    [Serializable]
    public class ESResRefer : ESResRefer<UnityEngine.Object>
    {

    }
    [Serializable]
    public class ESResReferAudioClip : ESResRefer<AudioClip>
    {

    }
    [Serializable]
    public class ESResReferPrefab : ESResRefer<GameObject>
    {

    }
    [Serializable]
    public class ESResReferMat : ESResRefer<Material>
    {

    }
#if UNITY_EDITOR
    public class ESResReferDrawer : OdinValueDrawer<ESResReferAB>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            EditorGUILayout.BeginHorizontal();
            label.image = EditorIcons.StarPointer.Raw;
            EditorGUILayout.LabelField(label,GUILayout.MaxWidth(200));
            this.ValueEntry.SmartValue.Draw();
            EditorGUILayout.EndHorizontal();
        }
    }
#endif
}
