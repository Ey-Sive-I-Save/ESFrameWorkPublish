using UnityEngine;
using System.Collections;

// 示例脚本：演示基本的MonoBehaviour功能
// 这个脚本展示了如何使用Unity的基础组件和生命周期方法
public class SampleScript : MonoBehaviour
{
    // 公共变量，可以在Inspector中调整
    public float speed = 5.0f; // 移动速度
    public Color objectColor = Color.white; // 对象颜色

    // 私有变量
    private Rigidbody rb;
    private Renderer rend;

    // Start is called before the first frame update
    void Start()
    {
        // 获取组件引用
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();

        // 设置初始颜色
        if (rend != null)
        {
            rend.material.color = objectColor;
        }

        Debug.Log("SampleScript: 对象初始化完成");
    }

    // Update is called once per frame
    void Update()
    {
        // 简单的移动逻辑（WASD控制）
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        transform.Translate(movement * speed * Time.deltaTime);

        // 空格键跳跃
        if (Input.GetKeyDown(KeyCode.Space) && rb != null)
        {
            rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
        }
    }

    // OnCollisionEnter is called when this collider/rigidbody has begun touching another rigidbody/collider
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("SampleScript: 碰撞到 " + collision.gameObject.name);
    }

    // 公共方法，可以被其他脚本调用
    public void ChangeColor(Color newColor)
    {
        objectColor = newColor;
        if (rend != null)
        {
            rend.material.color = objectColor;
        }
    }

    // 协程示例：延迟执行
    public IEnumerator DelayedAction(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("SampleScript: 延迟动作执行完成");
    }
}