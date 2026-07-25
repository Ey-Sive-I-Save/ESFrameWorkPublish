using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_VectorPicker : MonoBehaviour
    {
        public Material targetMaterial;
        public string propertyName;

        Slider slider1;
        Slider slider2;

        void Start()
        {
            if(slider1 == null)
            {
                //References:
                slider1 = transform.Find("Slider 1").GetComponent<Slider>();
                slider2 = transform.Find("Slider 2").GetComponent<Slider>();
            }
        }

        public void SetTarget(Material newMaterial, string newProperty, string shaderName)
        {
            Start();

            targetMaterial = newMaterial;
            propertyName = newProperty;

            //Limits:
            Vector2 vectorValue = targetMaterial.GetVector(propertyName);
            int propIndex = targetMaterial.shader.FindPropertyIndex(propertyName);


            float absValue = (Mathf.Abs(vectorValue.x) + Mathf.Abs(vectorValue.y) * 0.5f);

            if (absValue < 1f)
            {
                slider1.minValue = slider2.minValue = -1;
                slider1.maxValue = slider2.maxValue = 1f;
            } else if (absValue < 2f)
            {
                slider1.minValue = slider2.minValue = -2;
                slider1.maxValue = slider2.maxValue = 2f;
            }
            else
            {
                slider1.minValue = slider2.minValue = -absValue * 2;
                slider1.maxValue = slider2.maxValue = absValue * 2f;
            }

            if(propertyName.EndsWith("Scale"))
            {
                slider1.minValue = slider2.minValue  = 0f;
            }

            //Load:
            LoadVector(vectorValue);

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

        public void LoadVector(Vector2 vectorValue)
        {
            slider1.SetValueWithoutNotify(vectorValue.x);
            slider2.SetValueWithoutNotify(vectorValue.y);
            UpdateVector(vectorValue);
        }

        public void UpdateVector(Vector2 vectorValue)
        {
            //String:
            SetSliderValue(slider1, vectorValue.x);
            SetSliderValue(slider2, vectorValue.y);

            //Material:
            if (targetMaterial != null)
            {
                targetMaterial.SetVector(propertyName, vectorValue);
            }
        }

        void SetSliderValue(Slider toSlider, float toValue)
        {
            string floatString = toValue.ToString().Replace(",", ".");
            string[] splitValues = floatString.Split('.');
            if (splitValues.Length > 1)
            {
                floatString = splitValues[0] + "." + splitValues[1].Substring(0, Mathf.Min(splitValues[1].Length, Mathf.Abs(toValue) >= 0.01f ? 2 : 3));
            }
            toSlider.transform.Find("Value").GetComponent<Text>().text = floatString;
        }

        public void SliderChanged()
        {
            UpdateVector(new Vector2(slider1.value, slider2.value));
        }
    }
}