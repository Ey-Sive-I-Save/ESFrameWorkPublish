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

  /// <summary>
  /// 轨道顺序的正式可变协议。编辑器必须通过本协议调整顺序，
  /// 不得反射或直接猜测具体序列实现中的列表字段。
  /// </summary>
  public interface ITrackSequenceMutableOrder
  {
    int TrackItemCount { get; }
    int IndexOfTrackItem(ITrackItem item);
    bool TryMoveTrackItem(ITrackItem item, int targetFinalIndex);
  }

  public abstract class TrackSequenceBase<ItemType> : ITrackSequence, ITrackSequenceDurationCache, ITrackSequenceMutableOrder where ItemType : class, ITrackItem
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
    public int TrackItemCount => tracks_.Count;

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

    public int IndexOfTrackItem(ITrackItem item)
    {
      return item is ItemType typedItem ? tracks_.IndexOf(typedItem) : -1;
    }

    public bool TryMoveTrackItem(ITrackItem item, int targetFinalIndex)
    {
      if (!(item is ItemType typedItem))
        return false;

      int oldIndex = tracks_.IndexOf(typedItem);
      if (oldIndex < 0 || targetFinalIndex < 0 || targetFinalIndex >= tracks_.Count || targetFinalIndex == oldIndex)
        return false;

      tracks_.RemoveAt(oldIndex);
      tracks_.Insert(targetFinalIndex, typedItem);
      return true;
    }

    public abstract void InitByEditor();
  }

}
