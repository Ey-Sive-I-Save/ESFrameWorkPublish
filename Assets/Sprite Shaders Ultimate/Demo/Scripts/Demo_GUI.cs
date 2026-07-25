using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_GUI : MonoBehaviour
    {
        public static Demo_GUI instance;

        GameObject displayTitlePrefab;
        RectTransform propertyRect;
        CanvasGroup hudCG;
        Slider slider;

        float scrollArea;
        float targetHeight;

        void Awake()
        {
            instance = this;

            hudCG = transform.Find("Shader Hud").GetComponent<CanvasGroup>();
            displayTitlePrefab = transform.Find("Display Titles/Title Prefab").gameObject;
            propertyRect = transform.Find("Shader Hud/Properties/Rect").GetComponent<RectTransform>();
            slider = transform.Find("Shader Hud/Properties/Slider").GetComponent<Slider>();
        }

        void Update()
        {
            hudCG.alpha = Mathf.Lerp(hudCG.alpha, Demo_Shaders.instance.FadeInGUI() ? 1.1f : -0.1f, Time.unscaledDeltaTime * 7.5f);

            if(hudCG.alpha > 0.5f)
            {
                if (slider.gameObject.activeInHierarchy)
                {
                    float strength = 100f / Mathf.Abs(scrollArea - 500f);
                    if (Input.mouseScrollDelta.y > 0.01f)
                    {
                        slider.SetValueWithoutNotify(Mathf.Clamp01(slider.value - strength));
                        UpdateScroll();
                    }
                    else if (Input.mouseScrollDelta.y < -0.01)
                    {
                        slider.SetValueWithoutNotify(Mathf.Clamp01(slider.value + strength));
                        UpdateScroll();
                    }
                }

                propertyRect.anchoredPosition = new Vector2(0, Mathf.Lerp(propertyRect.anchoredPosition.y, targetHeight, Time.unscaledDeltaTime * 8f));
            }

        }

        public void CreateTitle(string title, Transform target)
        {
            GameObject newTitle = Instantiate<GameObject>(displayTitlePrefab);
            newTitle.transform.SetParent(displayTitlePrefab.transform.parent, true);
            newTitle.name = title;

            newTitle.GetComponent<Demo_DisplayTitle>().target = target;
            newTitle.GetComponent<Text>().text = title;
            newTitle.SetActive(true);
        }

        public void UpdateHud()
        {
            //Toggle Interaction:
            if (Demo_Display.selected == null)
            {
                hudCG.blocksRaycasts = hudCG.interactable = false;
                return;
            }
            hudCG.blocksRaycasts = hudCG.interactable = true;

            //Set Title:
            Transform hud = transform.Find("Shader Hud");
            hud.Find("Shader Title").GetComponent<Text>().text = Demo_Display.selected.gameObject.name;

            //Alternative Button:
            float currentY = 240f;
            if (Demo_Display.selected.HasAlternatives())
            {
                propertyRect.Find("AlternativeButton").gameObject.SetActive(true);
                currentY = 240f - 60f;
            }
            else
            {
                propertyRect.Find("AlternativeButton").gameObject.SetActive(false);
            }

            //Properties:
            Transform properties = hud.Find("Properties");
            GameObject colorPrefab = properties.Find("Color").gameObject;
            colorPrefab.SetActive(false);
            GameObject floatPrefab = properties.Find("Float").gameObject;
            floatPrefab.SetActive(false);
            GameObject vectorPrefab = properties.Find("Vector").gameObject;
            vectorPrefab.SetActive(false);

            //Clear Properties:
            for (int c = 0; c < propertyRect.childCount; c++)
            {
                Transform child = propertyRect.GetChild(c);

                if(child.gameObject.name != "AlternativeButton")
                {
                    Destroy(propertyRect.GetChild(c).gameObject);
                }
            }

            //Create Properties:
            Material mat = Demo_Display.selected.InstantiateMaterial();
            if (mat == null)
            {
                slider.gameObject.SetActive(false);
                return;
            }

            int index = mat.shader.FindPropertyIndex(Demo_Display.selected.firstProperty);
            int maxCount = mat.shader.GetPropertyCount();
            bool hidden = false;
            while(index < maxCount)
            {
                string propertyName = mat.shader.GetPropertyName(index);
                ShaderPropertyType propertyType = mat.shader.GetPropertyType(index);
                index++;
                if (propertyName.StartsWith("_Enable"))
                {
                    break;
                }
                else
                {
                    if (!IsKeyword(propertyName))
                    {
                        if (hidden)
                        {
                            if (propertyName == "_EnchantedLowColor" || propertyName == "_EnchantedHighColor" || propertyName == "_ShiftingColorA" || propertyName == "_ShiftingColorB")
                            {
                                //No Skipping
                            }
                            else
                            {
                                continue; //Skip hidden properties.
                            }
                        }
                        else
                        {
                            if (propertyName == "_EnchantedLowColor" || propertyName == "_EnchantedHighColor" || propertyName == "_ShiftingColorA" || propertyName == "_ShiftingColorB")
                            {
                                continue; //Skipping if not Hidden
                            }
                        }

                        RectTransform newRect = null;
                        if (propertyType == ShaderPropertyType.Color)
                        {
                            GameObject newColor = Instantiate<GameObject>(colorPrefab);
                            newColor.transform.SetParent(propertyRect, true);
                            newColor.transform.position = colorPrefab.transform.position;
                            newColor.transform.localScale = Vector3.one;
                            newColor.SetActive(true);

                            newRect = newColor.GetComponent<RectTransform>();
                            newColor.GetComponent<Demo_ColorPicker>().SetTarget(mat, propertyName, Demo_Display.selected.gameObject.name);
                        }
                        else if (propertyType == ShaderPropertyType.Float || propertyType == ShaderPropertyType.Range)
                        {
                            GameObject newFloat = Instantiate<GameObject>(floatPrefab);
                            newFloat.transform.SetParent(propertyRect, true);
                            newFloat.transform.position = floatPrefab.transform.position;
                            newFloat.transform.localScale = Vector3.one;
                            newFloat.SetActive(true);

                            newRect = newFloat.GetComponent<RectTransform>();
                            newFloat.GetComponent<Demo_FloatPicker>().SetTarget(mat, propertyName, Demo_Display.selected.gameObject.name);
                        }
                        else if (propertyType == ShaderPropertyType.Vector)
                        {
                            GameObject newVector = Instantiate<GameObject>(vectorPrefab);
                            newVector.transform.SetParent(propertyRect, true);
                            newVector.transform.position = vectorPrefab.transform.position;
                            newVector.transform.localScale = Vector3.one;
                            newVector.SetActive(true);

                            newRect = newVector.GetComponent<RectTransform>();
                            newVector.GetComponent<Demo_VectorPicker>().SetTarget(mat, propertyName, Demo_Display.selected.gameObject.name);
                        }

                        if (newRect != null)
                        {
                            Vector2 anchoredPosition = newRect.anchoredPosition;
                            anchoredPosition.y = currentY - newRect.sizeDelta.y * 0.5f;
                            currentY -= newRect.sizeDelta.y;
                            newRect.anchoredPosition = anchoredPosition;
                        }
                    }
                    else
                    {
                        hidden = mat.GetFloat(propertyName) < 0.5f;
                    }
                }
            }

            scrollArea = 240f - currentY;
            slider.SetValueWithoutNotify(0f);
            slider.gameObject.SetActive(scrollArea > 500f);
            targetHeight = 0f;
        }

        public static bool IsKeyword(string propName)
        {
            if (propName.StartsWith("_Toggle") || propName.EndsWith("Toggle") || propName.EndsWith("Invert") || propName == "PixelSnap" || propName == "_ShaderSpace" || propName == "_SmokeVertexSeed" || propName == "_ShaderFading" || propName == "_BakedMaterial" || propName == "_SpriteSheetFix" || propName == "_ForceAlpha" || propName == "_VertexTintFirst" || propName == "_PixelPerfectSpace" || propName == "_PixelPerfectUV" || propName == "_WindLocalWind" || propName == "_WindHighQualityNoise" || propName == "_WindIsParallax" || propName == "_WindFlip" || propName == "_SquishFlip")
            {
                return true;
            }

            return false;
        }

        //Buttons:
        public void BackButton()
        {
            if(Demo_Display.selected != null)
            {
                Demo_Display.selected.Deselect();
            }
        }

        public void ResetMaterialButton()
        {
            if (Demo_Display.selected != null)
            {
                UpdateHud();
            }
        }

        public void AlternativeButton()
        {
            if(Demo_Display.selected != null)
            {
                Demo_Display.selected.InstantiateMaterial();
                Demo_Display.selected.ChangeIndex();
                UpdateHud();
            }
        }

        public void UpdateScroll()
        {
            targetHeight = slider.value * (scrollArea - 500f);
        }

    }
}

