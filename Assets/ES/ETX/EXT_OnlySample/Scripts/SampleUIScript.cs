using UnityEngine;
using UnityEngine.UI; // 需要Unity UI包

public class SampleUIScript : MonoBehaviour
{
    // UI组件引用
    public Button button;
    public Text text;
    public Slider slider;

    // 事件委托
    public delegate void OnButtonClicked();
    public event OnButtonClicked ButtonClickedEvent;

    void Start()
    {
        // 设置UI事件监听器
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }

        if (slider != null)
        {
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        // 初始化文本
        if (text != null)
        {
            text.text = "欢迎使用ES框架示例包！";
        }

        Debug.Log("SampleUIScript: UI初始化完成");
    }

    // 按钮点击事件处理
    private void OnButtonClick()
    {
        Debug.Log("SampleUIScript: 按钮被点击");

        // 触发自定义事件
        ButtonClickedEvent?.Invoke();

        // 更新文本
        if (text != null)
        {
            text.text = "按钮已被点击！";
        }
    }

    // 滑块值改变事件处理
    private void OnSliderValueChanged(float value)
    {
        Debug.Log("SampleUIScript: 滑块值改变为 " + value);

        // 根据滑块值更新文本颜色
        if (text != null)
        {
            text.color = Color.Lerp(Color.red, Color.green, value);
        }
    }

    // 公共方法：重置UI
    public void ResetUI()
    {
        if (text != null)
        {
            text.text = "UI已重置";
            text.color = Color.white;
        }

        if (slider != null)
        {
            slider.value = 0.5f;
        }

        Debug.Log("SampleUIScript: UI已重置");
    }

    // 协程：文本闪烁效果
    public System.Collections.IEnumerator BlinkText(float duration)
    {
        float timer = 0f;
        bool isVisible = true;

        while (timer < duration)
        {
            if (text != null)
            {
                text.enabled = isVisible;
                isVisible = !isVisible;
            }

            yield return new WaitForSeconds(0.5f);
            timer += 0.5f;
        }

        if (text != null)
        {
            text.enabled = true;
        }
    }
}