using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface ITrackItem
    {
        public bool Enabled{get;set;}
        public IEnumerable<ITrackNode> Nodes{get;}
        public Color ItemBGColor{get;}
    }
    
}
