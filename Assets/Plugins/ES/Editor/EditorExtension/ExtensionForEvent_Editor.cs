using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public static class ExtensionForEvent_Editor
    {
        public static bool _DragDownAt(this Event _ev,Rect rect)
        {
            return true;
        }
    }
}
