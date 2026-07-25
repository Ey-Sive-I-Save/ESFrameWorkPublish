using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_ColorPicker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Material targetMaterial;
        public string propertyName;

        RectTransform colorArea;
        Slider brightnessSlider;
        RectTransform dotRect;
        Image dotImage;

        bool isHovered;
        bool isDragging;

        float lastHue;
        float lastSaturation;
        float maxBrightness;

        void Start()
        {
            if(brightnessSlider == null || colorArea == null)
            {
                //References:
                colorArea = transform.Find("Color Area").GetComponent<RectTransform>();
                brightnessSlider = transform.Find("Brightness Slider").GetComponent<Slider>();
                dotRect = transform.Find("Color Area/Dot").GetComponent<RectTransform>();
                dotImage = dotRect.GetComponent<Image>();

                //Initialize:
                dotImage.material = Instantiate<Material>(dotImage.material);
            }
        }

        void Update()
        {
            Vector2 mousePosition = default;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(colorArea, Input.mousePosition, Camera.main, out mousePosition);

            if (isHovered && Input.GetMouseButtonDown(0))
            {
                if (mousePosition.x > -colorArea.sizeDelta.x * 0.5f && mousePosition.x < colorArea.sizeDelta.x * 0.5f && mousePosition.y > -colorArea.sizeDelta.y * 0.5f && mousePosition.y < colorArea.sizeDelta.y * 0.5f)
                {
                    isDragging = true;
                }
            }

            if(isDragging)
            {
                if(Input.GetMouseButton(0) == false)
                {
                    isDragging = false;
                }

                Vector2 colorRange = (mousePosition + colorArea.sizeDelta * 0.5f) / colorArea.sizeDelta;
                float hue = Mathf.Clamp01(colorRange.x);
                float saturation = Mathf.Clamp01(colorRange.y);
                UpdateColor(hue, saturation);
            }
        }

        public void SetTarget(Material newMaterial, string newProperty, string shaderName)
        {
            Start();

            targetMaterial = newMaterial;
            propertyName = newProperty;

            LoadColor(targetMaterial.GetColor(propertyName));

            //Title:
            string displayString = newProperty.Replace("_" + shaderName.Replace(" ", ""), "");
            char[] displayChars = displayString.ToCharArray();
            displayString = "";
            for(int c = 0; c < displayChars.Length; c++)
            {
                if(c > 0 && displayChars[c].ToString().ToUpper() == displayChars[c].ToString())
                {
                    displayString += " ";
                }

                displayString += displayChars[c];
            }
            transform.Find("Title").GetComponent<Text>().text = displayString;
        }

        public void LoadColor(Color color)
        {
            float hue;
            float saturation;
            float value;
            Color.RGBToHSV(color, out hue, out saturation, out value);

            maxBrightness = Mathf.Ceil(value * 0.5f) * 4f + 5f;

            if(value <= 1f)
            {
                brightnessSlider.SetValueWithoutNotify(value * 0.5f);
            }
            else
            {
                brightnessSlider.SetValueWithoutNotify(0.5f + (value - 1f) / maxBrightness);
            }

            UpdateColor(hue, saturation);
        }

        public void UpdateColor(float hue, float saturation)
        {
            lastHue = hue;
            lastSaturation = saturation;

            dotRect.anchoredPosition = new Vector2(Mathf.Clamp(colorArea.sizeDelta.x * hue, 5, colorArea.sizeDelta.x - 5), Mathf.Clamp(colorArea.sizeDelta.y * saturation, 5, colorArea.sizeDelta.y - 5));
            dotImage.color = Color.HSVToRGB(hue, saturation, 1f);

            float value = Mathf.Min(brightnessSlider.value * 2f, 1) + Mathf.Max((brightnessSlider.value - 0.5f) * maxBrightness, 0);
            dotImage.materialForRendering.SetFloat("_Brightness", value);

            if(targetMaterial != null)
            {
                targetMaterial.SetColor(propertyName, Color.HSVToRGB(hue, saturation, value));
            }
        }

        public void SliderChanged()
        {
            if (Mathf.Abs(brightnessSlider.value - 0.5f) < 0.05f)
            {
                brightnessSlider.SetValueWithoutNotify(0.5f);
            }

            UpdateColor(lastHue, lastSaturation);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }


    }
}