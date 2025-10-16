using System;
using UnityEngine;
using UnityEngine.UI;
namespace FlyRabbit.EventCenter
{
    public class Test : MonoBehaviour
    {
        public Text OutputText;

        public InputField Test1Param1;
        public Button Test1Button;

        public InputField Test2Param1;
        public InputField Test2Param2;
        public Button Test2Button;

        private void Awake()
        {
            Test1Button.onClick.AddListener(OnTest1ButtonClick);
            Test2Button.onClick.AddListener(OnTest2ButtonClick);
        }

        private void OnEnable()
        {
            EventCenter.AddListener<string>(EventName.Test1, Listener1);
            EventCenter.AddListener<string>(EventName.Test1, Listener2);
            EventCenter.AddListener<int, int>(EventName.Test2, Listener3);
            EventCenter.AddListener<int, int>(EventName.Test2, Listener4);
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<string>(EventName.Test1, Listener1);
            EventCenter.RemoveListener<string>(EventName.Test1, Listener2);
            EventCenter.RemoveListener<int, int>(EventName.Test2, Listener3);
            EventCenter.RemoveListener<int, int>(EventName.Test2, Listener4);
        }

        #region ButtonClick

        private void OnTest1ButtonClick()
        {
            OutputText.text = "";
            EventCenter.TriggerEvent<int>(EventName.Test1, 0);
            EventCenter.TriggerEvent<string>(EventName.Test1, Test1Param1.text);
        }

        private void OnTest2ButtonClick()
        {
            OutputText.text = "";
            EventCenter.TriggerEvent<int, int>(EventName.Test2, Convert.ToInt32(Test2Param1.text), Convert.ToInt32(Test2Param2.text));
        }

        #endregion

        private void Error1()
        {
           
        }
        private void Error2()
        {
           EventCenter.AddListener<string>(EventName.Test2, Listener1);
        }

        private void Listener1(string text)
        {
            OutputText.text += $"Listener1接收到了:{text}\n";
        }
        private void Listener2(string text)
        {
            OutputText.text += $"Listener2接收到了:{text}\n";
        }

        private void Listener3(int a, int b)
        {
            OutputText.text += $"Listener3接收到了:{a}和{b},进行相加，等于:{a + b}\n";
        }

        private void Listener4(int a, int b)
        {
            OutputText.text += $"Listener4接收到了:{a}和{b},进行相乘，等于:{a * b}\n";
        }
    }
}