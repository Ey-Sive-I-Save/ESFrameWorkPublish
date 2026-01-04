using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
  public interface ITrackSequence
  {
    public IEnumerable<ITrackItem> Tracks { get; }
    public bool TryAddTrackItem(ITrackItem item);
    public bool TryRemoveTrackItem(ITrackItem item);
    void InitByEditor();//被初始化按钮点击

    // public IEnumerable<>
  }

  public abstract class TrackSequenceBase<ItemType> : ITrackSequence where ItemType : class, ITrackItem
  {
    [SerializeReference]
    public List<ItemType> tracks_ = new();

    public IEnumerable<ITrackItem> Tracks => tracks_;

    public bool TryAddTrackItem(ITrackItem item)
    {
      if (item is ItemType tItem)
      {
        if (!tracks_.Contains(tItem))
        {
          Debug.Log("添加轨道项："+item.GetType()+item.DisplayName);
          tracks_.Add(tItem);
          return true;
        }
      }
      return false;
    }

    public bool TryRemoveTrackItem(ITrackItem item)
    {
      if (item is ItemType tItem)
      {
        if (tracks_.Contains(tItem))
        {
          tracks_.Remove(tItem);
          return true;
        }
      }
      return false;
    }

    public abstract void InitByEditor();
  }

}
