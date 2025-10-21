using ES;
using PlasticPipe.Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace ES
{
    //Go  Scriptable 都可以作为
    public interface INodeContainer
    {
        public NodeEnvironment environment { get; }
        public INodeRunner AddNodeByType(Type t);
        public IEnumerable<INodeRunner> GetAllNodes();
        
        public void RemoveRunner(INodeRunner runner);

        public INodeRunner CopyNodeRunner(INodeRunner runner);

    }
    
}
public static class EXT
{
    public static bool IsNotNull(this INodeContainer container)
    {
        if (container is UnityEngine.Object uo) return uo != null;
        return container != null;
    }
    public static bool IsNotNull(this INodeRunner runner)
    {
        if (runner is UnityEngine.Object uo) return uo != null;
        return runner != null;
    }
}
