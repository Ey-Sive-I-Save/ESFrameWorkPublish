using System;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UnityGui 
{ 
    public class GuiScaler 
    {
        // 160 is the dpi of the 1st generation iPhone, a good base value.
        private const float BaseScale = 320.0f;
        private bool _initialized;
        private bool _scaling;
        private Vector3 _guiScale = Vector3.one;
        private Matrix4x4 _restoreMatrix = Matrix4x4.identity;

        public GuiScaler()
        {
            Initialize();
        }

        public GuiScaler(float scale)
        {
            Initialize(scale);
        }

        public float Scale => _guiScale.x;

        /// <summary>
        /// Initialize the gui scaler with a specific scale.
        /// </summary>
        private void Initialize(float scale) 
        {
            if (_initialized) 
                return;
            
            _initialized = true;

            // scale will be 0 on platforms that have unknown dpi (usually non-mobile)
            // if the scale is less than 10% don't bother, it just makes gui look bad.
            if (Math.Abs(scale) < 0.000001f || scale < 1.1f) 
                return;

            _guiScale.Set(scale, scale, scale);
            _scaling = true;
        }

        private void Initialize() 
        {
            Initialize(Screen.dpi / BaseScale);
        }

        public void ReScale(float scale)
        {
            _scaling = true;
            _guiScale.Set(scale, scale, scale);
            var matrix = _restoreMatrix;
            GUI.matrix = matrix * Matrix4x4.Scale(_guiScale);
        }

        public void Begin() 
        {
            if (!_initialized) 
                Initialize();

            if (!_scaling) 
                return;

            _restoreMatrix = GUI.matrix;
            GUI.matrix = GUI.matrix * Matrix4x4.Scale(_guiScale);
        }

        public void End() 
        {
            if (!_scaling) 
                return;
            GUI.matrix = _restoreMatrix;
        }
    }
}