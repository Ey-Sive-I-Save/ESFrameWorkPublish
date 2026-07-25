using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_SpriteFader : MonoBehaviour
    {
        SpriteRenderer sr;
        bool fadeIn;

        float fadeDelay;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            fadeIn = true;
            fadeDelay = 0f;
        }

        void Update()
        {
            //Delay:
            if(fadeDelay > 0)
            {
                fadeDelay -= Time.unscaledDeltaTime;
                return;
            }

            //Fading:
            Color color = sr.color;
            color.a = Mathf.Clamp01(Mathf.Lerp(color.a, fadeIn ? 1.1f : -0.1f, Time.deltaTime * (fadeIn ? 8f : 4f)));
            sr.color = color;

            if((fadeIn && color.a >= 1f) || (!fadeIn && color.a <= 0f))
            {
                enabled = false;
            }
        }

        public void SetFade(bool fadeState)
        {
            fadeIn = fadeState;
            enabled = true;

            if(!fadeIn)
            {
                fadeDelay = 0.15f;
            }

            sr.sortingOrder = fadeIn ? 4 : 5;
        }
    }
}

