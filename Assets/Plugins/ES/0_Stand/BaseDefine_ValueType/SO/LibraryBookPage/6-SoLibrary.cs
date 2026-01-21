using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface IESLibrary : IString
    {

    }

    public interface IESPage : IString
    {
        public bool Draw();
    }
    public abstract class LibrarySoBase<Book> : ESSO, IESLibrary
    {
        [LabelText("Library名字")]
        public string Name = "Library PreNameToABKeys";

        [LabelText("包含")]
        public List<Book> Books = new List<Book>();

        [LabelText("描述")]
        public string Desc = "";
        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }
    }
    [Serializable]
    public abstract class BookBase<TPage> : IString where TPage : PageBase, IString, new()
    {
        [LabelText("Book名字")]
        public string Name = "book PreNameToABKeys";

        /// <summary>
        /// 对当前 Book 的补充说明，例如“战斗相关资源”“登录场景资源”等，
        /// 仅用于编辑器标记与文档，可结合构建管线生成报表。
        /// </summary>
        [LabelText("描述")]
        public string Desc = "";
        [LabelText("收容页面")]
        [SerializeField]
        public List<TPage> pages = new List<TPage>();

        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }

        public virtual void EditorOnly_DragAtArea(UnityEngine.Object[] gs)
        {
            foreach (var i in gs)
            {
                if (i != null) pages.Add(CreateNewPage(i));
            }
        }

        public virtual TPage CreateNewPage(UnityEngine.Object uo)
        {

            return new TPage() { Name = uo.name };
        }

    }


    [Serializable]
    public abstract class PageBase : IESPage, IString
    {
        [LabelText("资源页名")]
        public string Name = "资源页名";
        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }

        public virtual bool Draw()
        {
            return false;
        }

    }

}
