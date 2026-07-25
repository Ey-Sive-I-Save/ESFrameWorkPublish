using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SpriteShadersUltimate.Demo
{
    public class Demo_Trigger : MonoBehaviour
    {
        ShaderFaderSSU fader;

        public List<Demo_TriggerEvent> events;

        void Start()
        {
            fader = GetComponent<ShaderFaderSSU>();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if(collision.name == "Player")
            {
                ChangeState(true);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.name == "Player")
            {
                ChangeState(false);
            }
        }

        public void ChangeState(bool isActive)
        {
            if (fader != null)
            {
                fader.isFaded = isActive;
            }

            if (events != null && isActive)
            {
                foreach (Demo_TriggerEvent demoEvent in events)
                {
                    StartCoroutine(PlayEvent(demoEvent));
                }
            }
        }

        IEnumerator PlayEvent(Demo_TriggerEvent demoEvent)
        {
            yield return new WaitForSeconds(demoEvent.delay);
            demoEvent.Play(transform);
        }
    }

    [System.Serializable]
    public class Demo_TriggerEvent
    {
        [Header("Delay:")]
        public float delay;

        [Header("Change Fader:")]
        public ShaderFaderSSU fader;
        public bool faderState;
        public bool negateState;

        [Header("Snap Player:")]
        public bool snapPlayer;
        public bool isRelative;
        public Vector3 snapPosition;

        [Header("Hurt Player:")]
        public bool hurtPlayer;
        public Vector2 velocity;

        public void Play(Transform source)
        {
            if(fader != null)
            {
                if(negateState)
                {
                    fader.isFaded = !fader.isFaded;
                }
                else
                {
                    fader.isFaded = faderState;
                }
            }

            if(snapPlayer)
            {
                Demo_Player.instance.SnapPosition(isRelative ? source.position + snapPosition : snapPosition);
            }

            if(hurtPlayer)
            {
                Demo_Player.instance.GetHurt(velocity);
            }
        }
    }
}