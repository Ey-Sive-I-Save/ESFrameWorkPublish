using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class ERC_NodeRunnerSoMenu : EditorRegister_FOR_ClassAttribute<CreateNodeRunnerSoMenuAttribute>
    {
        public override void Handle(CreateNodeRunnerSoMenuAttribute attribute, Type type)
        {
            var flags= ExtensionForEnum._GetEnumValues<NodeEnvironment>();
            ESNodeUtility.UseNodes.TryAdd( NodeEnvironment.None, (attribute.Group, attribute.Name, type));
            foreach (var i in flags)
            {
                ESNodeUtility.UseNodes.TryAdd(i, (attribute.Group, attribute.Name, type));
            }
           
        }
    }
}
