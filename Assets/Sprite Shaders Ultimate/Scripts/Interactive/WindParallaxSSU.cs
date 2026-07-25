using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteShadersUltimate
{
    [AddComponentMenu("Sprite Shaders Ultimate/Wind/Wind Parallax")]
    public class WindParallaxSSU : MonoBehaviour
    {
        float originalXPosition;

        void Awake()
        {
            originalXPosition = transform.position.x;
        }

        void Start()
        {
            GetComponent<Renderer>().material.SetFloat("_WindXPosition", originalXPosition);
        }
    }
}