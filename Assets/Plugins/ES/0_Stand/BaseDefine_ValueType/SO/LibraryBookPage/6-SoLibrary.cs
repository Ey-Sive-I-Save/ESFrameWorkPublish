using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES {
    public interface ESLibrary :IString
    {
        
    }
    public abstract class SoLibrary<Book> : ESSO,ESLibrary
    {
        [LabelText("Library名字")]
        public string Name = "Library PreNameToABKeys";
        [LabelText("包含")]
        public List<Book> Books = new List<Book>();

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
    public abstract class Book<Page> : IString where Page:IString
    {
        [LabelText("Book名字")]
        public string Name = "book PreNameToABKeys";
        [LabelText("收容页面")]
        [SerializeField]
        public List<Page> pages = new List<Page>();
        
        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }
    }
}
