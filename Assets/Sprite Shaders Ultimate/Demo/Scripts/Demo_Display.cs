using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_Display : MonoBehaviour
    {
        //Selected:
        public static Demo_Display selected;

        [Header("Material Settings:")]
        public string firstProperty = "";
        public bool ignorePlayer = false;
        public float cycleTime = 2f;

        //Runtime:
        Transform shader;
        Material frameMaterial;
        bool isHovered;
        float lastScale;
        float activeUntil;
        int mainIndex;

        //Material:
        Renderer mainRenderer;
        Material mainMaterial;
        Transform extraSprites;

        //Position:
        int row;
        int slot;
        int maxSlots;
        float camWidth;

        void Start()
        {
            //Initialize:
            frameMaterial = transform.Find("Display/Frame").GetComponent<SpriteRenderer>().material;
            shader = transform.Find("Shader");
            isHovered = false;
            lastScale = 0f;
            activeUntil = Time.unscaledTime + 2f;

            //Position:
            int index = transform.GetSiblingIndex();
            row = index % 3 - 1;
            slot = index / 3;
            int parentCount = transform.parent.childCount;
            maxSlots = parentCount / 3 + (row + 1 < parentCount % 3 ? 1 : 0);
            camWidth = ((float)Screen.width / (float)Screen.height) * Camera.main.orthographicSize;
            UpdatePosition();
            UpdatePosition();

            //Material:
            mainIndex = 0;
            UpdateIndex();

            extraSprites = shader.Find("Extra Sprites");

            if (extraSprites != null)
            {
                extraSprites.localPosition = new Vector3(4f, 0, 0);
            }

            //First Property:
            if (firstProperty == null || firstProperty == "")
            {
                firstProperty = "_" + gameObject.name.Replace(" ", "") + "Fade";
            }

            //Title:
            Demo_GUI.instance.CreateTitle(gameObject.name, transform.Find("Display/Title Position"));

            //Coroutine:
            StartCoroutine(CycleShader());
            StartCoroutine(HandlePosition());
        }

        void Update()
        {
            if (Time.unscaledTime > activeUntil) return;

            float scale = transform.localScale.x;

            if (selected == this)
            {
                activeUntil = Time.unscaledTime + 4f;
                scale = Mathf.Clamp(Mathf.Lerp(scale, 1.11f, Time.unscaledDeltaTime * 5f), 1, 1.1f);
                shader.localScale = Vector3.Lerp(shader.localScale, Vector3.one * 1f / 8f, Time.unscaledDeltaTime * 10f);
                shader.localPosition = Vector3.Lerp(shader.localPosition, new Vector3(-0.3f, 0, 0), Time.unscaledDeltaTime * 10f);

                if(extraSprites != null)
                {
                    extraSprites.localPosition = Vector3.Lerp(extraSprites.localPosition, new Vector3(0, 0, 0), Time.unscaledDeltaTime * 10f);
                }
            }
            else
            {
                scale = Mathf.Clamp(Mathf.Lerp(scale, isHovered && selected == null ? 1.11f : 0.99f, Time.unscaledDeltaTime * 5f), 1, 1.1f);
                shader.localScale = Vector3.Lerp(shader.localScale, new Vector3(0.45f, 0.45f, 1f), Time.unscaledDeltaTime * 4f);
                shader.localPosition = Vector3.Lerp(shader.localPosition, new Vector3(0f, 0.45f, 0f), Time.unscaledDeltaTime * 4f);

                if (extraSprites != null)
                {
                    extraSprites.localPosition = Vector3.Lerp(extraSprites.localPosition, new Vector3(2f, 0, 0), Time.unscaledDeltaTime * 10f);
                }
            }

            if (scale != lastScale)
            {
                lastScale = scale;
                transform.localScale = new Vector3(scale, scale, 1);
                frameMaterial.SetFloat("_SineGlowFade", (scale - 1f) * 10f);
            }
        }

        IEnumerator CycleShader()
        {
            yield return new WaitForSeconds(transform.GetSiblingIndex() * 0.01f);

            while (true)
            {
                yield return new WaitForSeconds(cycleTime);

                if(selected != this)
                {
                    ChangeIndex();
                }
            }
        }
        IEnumerator HandlePosition()
        {
            yield return new WaitForSeconds(transform.GetSiblingIndex() * 0.01f);

            while (true)
            {
                yield return new WaitForSeconds(0.2f);

                UpdatePosition();
            }
        }

        Transform GetMainSprite(int index)
        {
            if (index <= 0)
            {
                return shader.Find("Main Sprite");
            }
            else
            {
                return shader.Find("Main Sprite " + (int) (index + 1));
            }
        }

        public void ChangeIndex()
        {
            mainIndex++;
            if(GetMainSprite(mainIndex) == null)
            {
                mainIndex = 0;
            }

            UpdateIndex();
        }

        public bool HasAlternatives()
        {
            return shader.Find("Main Sprite 2") != null;
        }

        public void UpdateIndex()
        {
            for (int i = 0; i < 6; i++)
            {
                Transform sprite = GetMainSprite(i);

                if (sprite != null)
                {
                    Demo_SpriteFader dsf = sprite.GetComponent<Demo_SpriteFader>();
                    if(dsf != null)
                    {
                        dsf.SetFade(false);
                    }
                    else
                    {
                        sprite.gameObject.SetActive(false);
                    }
                }
            }

            Transform mainSprite = GetMainSprite(mainIndex);
            if (mainSprite != null)
            {
                Demo_SpriteFader dsf = mainSprite.GetComponent<Demo_SpriteFader>();
                if (dsf != null)
                {
                    dsf.SetFade(true);
                }
                else
                {
                    mainSprite.gameObject.SetActive(true);
                }

                mainRenderer = mainSprite.GetComponent<Renderer>();
                mainMaterial = mainRenderer.material;
            }
        }

        void OnMouseOver()
        {
            isHovered = true;
            activeUntil = Time.unscaledTime + 4f;
        }

        void OnMouseExit()
        {
            isHovered = false;
            activeUntil = Time.unscaledTime + 4f;
        }

        void OnMouseDown()
        {
            if(selected == null)
            {
                Select();
            }
        }

        public void Select()
        {
            mainIndex = 0;
            UpdateIndex();
            activeUntil = Time.unscaledTime + 4f;
            selected = this;
            Demo_Player.instance.ResetPosition();
            Demo_GUI.instance.UpdateHud();
        }

        public void Deselect()
        {
            activeUntil = Time.unscaledTime + 4f;
            selected = null;
            ResetMaterial();
        }

        public void ResetMaterial()
        {
            if (mainRenderer != null && mainRenderer.material != null && mainRenderer.material != mainMaterial)
            {
                Destroy(mainRenderer.material);
                mainRenderer.material = mainMaterial;
            }
        }

        public Material InstantiateMaterial()
        {
            if (mainMaterial == null)
            {
                Demo_Player.instance.ResetMaterial();
                return null;
            }

            Material newMaterial = Instantiate(mainMaterial);
            mainRenderer.material = newMaterial;

            if (newMaterial != null && !ignorePlayer)
            {
                Demo_Player.instance.ApplyMaterial(newMaterial);
            }
            else
            {
                Demo_Player.instance.ResetMaterial();
            }

            return newMaterial;
        }

        void UpdatePosition()
        {
            float position = transform.position.x / Demo_Shaders.instance.transform.localScale.x;

            if (position < -camWidth * 1.4f)
            {
                slot += maxSlots;
            }else if(position > camWidth * 1.4f)
            {
                slot -= maxSlots;
            }

            transform.localPosition = new Vector3(2.75f * slot, -3.25f * row, 0);
        }
    }
}