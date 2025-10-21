using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public interface IFillInvoker<in Fill>
    {
        public void InvokeWithFill(Fill fill);
    }
}
