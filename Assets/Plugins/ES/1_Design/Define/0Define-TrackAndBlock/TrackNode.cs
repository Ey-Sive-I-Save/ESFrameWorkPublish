using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface ITrackNode
    {
        public string Name{get;set;}
        public float StartTime{get;set;}
        public float DurationTime{get;set;}
      // public IEnumerable<>
    }
    
}
