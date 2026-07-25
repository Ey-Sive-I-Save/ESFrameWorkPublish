using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_Camera : MonoBehaviour
    {
        void LateUpdate()
        {
            //Follow player's x position.
            Vector3 position = transform.position;
            position.x = Mathf.Lerp(position.x, Demo_Player.instance.transform.position.x, Time.deltaTime * 3f);
            transform.position = position;
        }
    }
}
