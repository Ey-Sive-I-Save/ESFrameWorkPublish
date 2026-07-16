using System;
using System.Collections;
using System.Collections.Generic;
using ES;
using UnityEngine;
namespace ES.EditorInternal
{
    public class ERC_NodeRunnerSoMenu : EditorRegister_FOR_ClassAttribute<CreateNodeRunnerSoMenuAttribute>
    {
        public override void Handle(CreateNodeRunnerSoMenuAttribute attribute, Type type)
        {
            var flags= ExtForEnum._GetEnumValues<NodeEnvironment>();
            ESNodeUtility.UseNodes.Add( NodeEnvironment.None, (attribute.Group, attribute.Name, type));
            foreach (var i in flags)
            {
                if(attribute.environment.HasFlag(i)&&i!= NodeEnvironment.None)
                ESNodeUtility.UseNodes.Add(i, (attribute.Group, attribute.Name, type));
            }
           
        }
    }
}
