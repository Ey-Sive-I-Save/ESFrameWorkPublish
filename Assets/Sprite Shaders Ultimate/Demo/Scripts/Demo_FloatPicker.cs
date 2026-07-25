using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_FloatPicker : MonoBehaviour
    {
        public Material targetMaterial;
        public string propertyName;

        Slider slider;

        void Start()
        {
            if(slider == null)
            {
                //References:
                slider = transform.Find("Slider").GetComponent<Slider>();
            }
        }

        public void SetTarget(Material newMaterial, string newProperty, string shaderName)
        {
            Start();

            targetMaterial = newMaterial;
            propertyName = newProperty;

            //Limits:
            float floatValue = targetMaterial.GetFloat(propertyName);
            int propIndex = targetMaterial.shader.FindPropertyIndex(propertyName);
            if (targetMaterial.shader.GetPropertyType(propIndex) == UnityEngine.Rendering.ShaderPropertyType.Range)
            {
                Vector2 limits = targetMaterial.shader.GetPropertyRangeLimits(propIndex);
                slider.minValue = limits.x;
                slider.maxValue = limits.y;
            }
            else
            {
                if (newProperty.EndsWith("Contrast"))
                {
                    slider.minValue = 0f;
                    slider.maxValue = 3f;
                }
                else if (newProperty.EndsWith("Saturation"))
                {
                    slider.minValue = 0f;
                    slider.maxValue = 2f;
                }
                else if (newProperty.EndsWith("Brightness"))
                {
                    slider.minValue = 0f;
                    slider.maxValue = 5f;
                }
                else if (newProperty.EndsWith("PixelDensity"))
                {
                    slider.minValue = 1f;
                    slider.maxValue = 32f;
                }
                else
                {
                    float limit = 1f;
                    while (Mathf.Abs(floatValue) > limit)
                    {
                        limit *= 10f;
                    }
                    slider.minValue = -limit;
                    slider.maxValue = limit;
                }
            }

            if (newProperty.EndsWith("Width"))
            {
                slider.minValue = 0f;
            }

            //Load:
            LoadFloat(floatValue);

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

            if (displayString == "") displayString = "Value";
            transform.Find("Title").GetComponent<Text>().text = displayString;
        }

        public void LoadFloat(float floatValue)
        {
            slider.SetValueWithoutNotify(floatValue);
            UpdateFloat(floatValue);
        }

        public void UpdateFloat(float floatValue)
        {
            //String:
            string floatString = floatValue.ToString().Replace(",", ".");
            string[] splitValues = floatString.Split('.');
            if (splitValues.Length > 1)
            {
                floatString = splitValues[0] + "." + splitValues[1].Substring(0, Mathf.Min(splitValues[1].Length, 2));
            }
            transform.Find("Value").GetComponent<Text>().text = floatString;

            if(targetMaterial != null)
            {
                targetMaterial.SetFloat(propertyName, floatValue);
            }
        }

        public void SliderChanged()
        {
            UpdateFloat(slider.value);
        }
    }
}