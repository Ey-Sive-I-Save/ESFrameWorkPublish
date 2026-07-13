using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
  public interface ITrackSequence
  {
    public string Name { get; }
    public IEnumerable<ITrackItem> Tracks { get; }
    public bool TryAddTrackItem(ITrackItem item);
    public bool TryRemoveTrackItem(ITrackItem item);
    void InitByEditor();//被初始化按钮点击

    // public IEnumerable<>
  }

  public interface ITrackSequenceDurationCache
  {
    float CachedMaxTime { get; set; }
  }

  public abstract class TrackSequenceBase<ItemType> : ITrackSequence, ITrackSequenceDurationCache where ItemType : class, ITrackItem
  {
    [TitleGroup("轨道序列", "保存时间轴中所有轨道项目。刷新轨道窗口时会自动更新缓存时长。")]
    [LabelText("轨道列表")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true, ShowIndexLabels = true)]
    [SerializeReference]
    public List<ItemType> tracks_ = new();

    [TitleGroup("轨道序列")]
    [ReadOnly]
    [LabelText("缓存最大时长")]
    [SuffixLabel("秒", true)]
    [SerializeField]
    private float cachedMaxTime = 10f;

    public IEnumerable<ITrackItem> Tracks => tracks_;
    public float CachedMaxTime { get => cachedMaxTime; set => cachedMaxTime = Mathf.Max(0f, value); }

        public abstract string Name { get; }

        public bool TryAddTrackItem(ITrackItem item)
    {
      if (item is ItemType tItem)
      {
        if (!tracks_.Contains(tItem))
        {
          Debug.Log("添加轨道项：" + item.GetType() + item.DisplayName);
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
