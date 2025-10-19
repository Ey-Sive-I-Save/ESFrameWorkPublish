using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    //Go  Scriptable 都可以作为
    public interface INodeContainer
    {
        public INodeRunner AddNodeByType(Type t);
        public IEnumerable<INodeRunner> GetAllNodes();

        public void RemoveRunner(INodeRunner runner);
    }
}
