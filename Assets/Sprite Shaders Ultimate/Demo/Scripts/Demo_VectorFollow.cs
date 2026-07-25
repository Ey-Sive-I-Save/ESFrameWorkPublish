using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_VectorFollow : MonoBehaviour
    {
        //Public:
        public string propertyName;
        public Transform trackedTransform;

        //Internal:
        Material mat;

        void Start()
        {
            Renderer renderer = GetComponentInChildren<Renderer>();

            if(renderer.sharedMaterial.name.EndsWith("(Instance)"))
            {
                mat = renderer.sharedMaterial;
            }
            else
            {
                mat = renderer.material;
            }
        }

        void FixedUpdate()
        {
            mat.SetVector(propertyName, trackedTransform.position);
        }
    }
}
