using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_DisplayTitle : MonoBehaviour
    {
        public Transform target;

        RectTransform rectTransform;
        RectTransform rectParent;

        void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            rectParent = transform.parent.GetComponent<RectTransform>();
        }

        void LateUpdate()
        {
            Vector3 screenPosition = RectTransformUtility.WorldToScreenPoint(Camera.main, target.position);

            
            Vector2 rectPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectParent, screenPosition, Camera.main, out rectPosition);

            rectTransform.anchoredPosition = rectPosition;

            transform.localScale = target.lossyScale;
        }
    }
}

