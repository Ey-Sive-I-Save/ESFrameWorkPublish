using Codice.Client.BaseCommands;
using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ES
{
    public class ESGraphViewSearchMenu : ScriptableObject, ISearchWindowProvider
    {
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            
            var entries = new List<SearchTreeEntry>();
            if (!ESGraphViewWindow.SContainer.IsNotNull()) {
                entries.Add(new SearchTreeGroupEntry(new GUIContent("未选中合适的容器")));
                return entries; }
            entries.Add(new SearchTreeGroupEntry(new GUIContent("创建新节点")));                //添加了一个一级菜单
           

            //USENODES
            var usenodes = ESNodeUtility.UseNodes;
            NodeEnvironment environment = ESGraphViewWindow.SContainer.environment;
            var items = usenodes.GetGroup(environment);
            HashSet<string> groupNames = new HashSet<string>();
            
            foreach (var item in items)
            {
                var groupName = item.Item1;
                if (groupNames.Contains(groupName))
                {

                }
                else
                {
                    groupNames.Add(groupName);
                    entries.Add(new SearchTreeGroupEntry(new GUIContent(groupName)){ level=1 });

                }
                entries.Add(new SearchTreeEntry(new GUIContent(item.Item2)) { level = 2, userData = item.Item3 });
            }

            return entries;
        }


        public delegate bool SerchMenuWindowOnSelectEntryDelegate(SearchTreeEntry searchTreeEntry, SearchWindowContext context);            //声明一个delegate类

        public SerchMenuWindowOnSelectEntryDelegate OnSelectEntryHandler;                              //delegate回调方法

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (OnSelectEntryHandler == null)
            {
                return false;
            }
            return OnSelectEntryHandler(searchTreeEntry, context);
        }
        private List<Type> GetClassList(Type type)
        {
            var q = type.Assembly.GetTypes()
                .Where(x => !x.IsAbstract)
                .Where(x => !x.IsGenericTypeDefinition)
                .Where(x => type.IsAssignableFrom(x));

            return q.ToList();
        }
    }
}

