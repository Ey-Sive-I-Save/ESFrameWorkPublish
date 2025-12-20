using Sirenix.OdinInspector;
using System;
using UnityEngine;
namespace ES
{
    public struct Link_MessagevalueEntrySwitch
    {
        public string key;
        public IValueEntry valueEntry;
        public bool isMain;
    }
    [Serializable, TypeRegistryItem("Message提供")]
    public class ESUIMessagevalueEntryDomain : Domain<ESUIElement, ESUIMessagevalueEntryModule>
    {
        [ShowInInspector, HideInEditorMode, LabelText("主信息提供源(运行时实时更换测试)")]
        public IValueEntry MainvalueEntry { get=> _mainvalueEntry;private set { if (_mainvalueEntry != value) {  _mainvalueEntry = value; if(EnableMainLink) DO_SendMainReaderLink(); } } }
        [HideInInspector]
        private IValueEntry _mainvalueEntry;
        [SerializeReference,LabelText("预注册主信息提供"),HideLabel,HideInPlayMode]
        public IValueEntryContainer RegisterMain;
        [ESBoolOption("禁用信息更新事件发送", "启用信息更新事件发送")]
        public bool EnableMainLink = true;
        /* [ShowInInspector, DisableInEditorMode,LabelText("常规备用读取器")]
         protected Dictionary<string, IMessagevalueEntry> Readers = new Dictionary<string, IMessagevalueEntry>();
         [ESBoolOption("禁用常规事件发送", "启用常规事件发射")]
         public bool EnableReadersLink = false;*/
        [ShowInInspector,ReadOnly]
        public LinkReceiveList<Link_MessagevalueEntrySwitch> LinkReceive = new LinkReceiveList<Link_MessagevalueEntrySwitch>();

        public void DO_SendMainReaderLink()
        {
             LinkReceive.SendLink(new Link_MessagevalueEntrySwitch() { key = "Main", valueEntry = _mainvalueEntry, isMain = true });
        }

        public void SetMainMessagevalueEntry(IValueEntry reader)
        {
            if (reader != _mainvalueEntry)
            {
                _mainvalueEntry = reader;
                if (EnableMainLink) DO_SendMainReaderLink();
            }
        }
    
        public IValueEntry GetMainMessagevalueEntry()
        {
            return _mainvalueEntry;
        }
        public override void _AwakeRegisterAllModules()
        {
            if (RegisterMain != null)
            {
                SetMainMessagevalueEntry(RegisterMain.GetValueEntry);
            }
            base._AwakeRegisterAllModules();
        }

        /* public IMessagevalueEntry GetKeyMessagevalueEntry(string key)
         {
             if (Readers.TryGetValue(key, out var v))
             {
                 if (v != null)
                 {
                     return v;
                 }
             }
             return null;
         }
         public void SetKeyMessagevalueEntry(string key, IMessagevalueEntry reader, bool AddIfNull = false)
         {
             if (Readers.TryGetValue(key, out var v))
             {
                 if (v != reader)
                 {
                     Readers[key] = reader;
                     LinkReceive.SendLink(new Link_MessagevalueEntry() { key = key, reader = reader, isMain = false });
                 }
             }
             else if (AddIfNull)
             {
                 Readers[key] = reader;
                 LinkReceive.SendLink(new Link_MessagevalueEntry() { key = key, reader = reader, isMain = false });
             }
         }
 */

    }
    [Serializable, TypeRegistryItem("UI信息提供源扩展模块")]
    public abstract class ESUIMessagevalueEntryModule : Module<ESUIElement, ESUIMessagevalueEntryDomain>
    {

    }
}
