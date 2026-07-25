using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_Shaders : MonoBehaviour
    {
        public static Demo_Shaders instance;
        public static float zoomFactor;

        GameObject environmentGO;
        List<SpriteRenderer> environmentSprites;
        Vector3 currentPosition;

        float lastZoomFactor;

        void Awake()
        {
            //Reference:
            instance = this;

            //Environment:
            Transform environment = GameObject.Find("Environment").transform;
            environmentSprites = new List<SpriteRenderer>();
            foreach(SpriteRenderer sr in environment.GetComponentsInChildren<SpriteRenderer>())
            {
                environmentSprites.Add(sr);
            }
            environmentGO = environment.gameObject;

            //Initialize:
            Demo_Display.selected = null;
            currentPosition = Vector3.zero;
            zoomFactor = 0f;
            lastZoomFactor = -1000;
        }

        void Update()
        {
            //Zoom Factor:
            if (Demo_Display.selected != null)
            {
                zoomFactor += Time.unscaledDeltaTime * 2f;
                if (zoomFactor > 1f) zoomFactor = 1f;
            }
            else
            {
                zoomFactor -= Time.unscaledDeltaTime * 2f;
                if (zoomFactor < 0f) zoomFactor = 0f;
            }

            //Scale:
            float scale = 1f + 6.2f * zoomFactor;
            transform.localScale = new Vector3(scale, scale, 1);

            if (zoomFactor != lastZoomFactor)
            {
                //Environment:
                float alpha = Mathf.Clamp01((zoomFactor - 0.75f) / 0.25f);
                foreach (SpriteRenderer sprite in environmentSprites)
                {
                    Color color = sprite.color;
                    color.a = alpha;
                    sprite.color = color;
                }

                if(alpha > 0f)
                {
                    if(!environmentGO.activeSelf)
                    {
                        environmentGO.SetActive(true);
                    }
                }
                else
                {
                    if (environmentGO.activeSelf)
                    {
                        environmentGO.SetActive(false);
                    }
                }

                //Other:
                lastZoomFactor = zoomFactor;
            }

            //Position:
            if(Demo_Display.selected != null)
            {
                currentPosition = Vector3.Lerp(currentPosition, -Demo_Display.selected.transform.localPosition, Time.unscaledDeltaTime * 10f);
            }
            else
            {

                float movement = 0f;
                if(AllowMovement())
                {
                    movement = 2f * (Screen.width * 0.5f - Input.mousePosition.x) / (float)Screen.width;
                    if(Mathf.Abs(movement) < 0.6f)
                    {
                        movement = 0;
                    }
                    else if (Input.mousePosition.x < Screen.width && Input.mousePosition.x > 0)
                    {
                        movement += movement < 0 ? 0.6f : -0.6f;
                        movement *= 2f;
                        movement = Mathf.Clamp(movement, -1f, 1f);
                    }
                    else
                    {
                        movement = 0;
                    }
                }

                currentPosition = Vector3.Lerp(currentPosition, new Vector3(currentPosition.x + movement, 0, 0), Time.unscaledDeltaTime * 14f / scale);
            }
            transform.position = currentPosition * scale;

            //Controls:
            if(Demo_Display.selected != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Demo_Display.selected.Deselect();
                }
            }
        }

        public bool AllowMovement()
        {
            return zoomFactor < 0.1f;
        }

        public bool FadeInGUI()
        {
            return zoomFactor > 0.9f;
        }
    }
}
