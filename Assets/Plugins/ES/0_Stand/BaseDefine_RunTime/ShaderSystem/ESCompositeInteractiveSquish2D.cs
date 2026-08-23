using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 通过独立 MPB 通道响应 2D 触发器接触，可与材质中的周期挤压叠加。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    [AddComponentMenu("【ES】/相机与表现/Composite Interactive Squish 2D")]
    public sealed class ESCompositeInteractiveSquish2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField, Range(-0.8f, 0.8f)] private float squishAmount = 0.35f;
        [SerializeField, Min(0f)] private float responseSpeed = 6f;
        [SerializeField] private bool staySquished = true;
        [SerializeField, Min(0f)] private float pulseDuration = 0.1f;

        private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
        private MaterialPropertyBlock propertyBlock;
        private float currentSquish;
        private float lastTriggerTime = float.NegativeInfinity;

        private void Reset()
        {
            ResolveTarget();
            BoxCollider2D interactionArea = GetComponent<BoxCollider2D>();
            if (interactionArea != null)
                interactionArea.isTrigger = true;
        }

        private void OnEnable()
        {
            ResolveTarget();
            contacts.Clear();
            currentSquish = 0f;
            lastTriggerTime = float.NegativeInfinity;
            WriteSquish(0f);
        }

        private void OnDisable()
        {
            contacts.Clear();
            currentSquish = 0f;
            lastTriggerTime = float.NegativeInfinity;
            WriteSquish(0f);
        }

        private void OnValidate()
        {
            squishAmount = Mathf.Clamp(squishAmount, -0.8f, 0.8f);
            responseSpeed = Mathf.Max(0f, responseSpeed);
            pulseDuration = Mathf.Max(0f, pulseDuration);
            ResolveTarget();
        }

        private void Update()
        {
            contacts.RemoveWhere(contact => contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy);
            bool active = staySquished
                ? contacts.Count > 0
                : Time.time <= lastTriggerTime + pulseDuration;
            float target = active ? squishAmount : 0f;
            float blend = responseSpeed <= 0f ? 1f : 1f - Mathf.Exp(-responseSpeed * Time.deltaTime);
            float nextSquish = Mathf.Lerp(currentSquish, target, blend);
            if (Mathf.Abs(nextSquish) < 0.0001f)
                nextSquish = 0f;
            if (Mathf.Approximately(nextSquish, currentSquish))
                return;

            currentSquish = nextSquish;
            WriteSquish(currentSquish);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            RegisterContact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other != null)
                contacts.Add(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null)
                contacts.Remove(other);
        }

        private void RegisterContact(Collider2D other)
        {
            if (other == null)
                return;
            contacts.Add(other);
            lastTriggerTime = Time.time;
        }

        private void WriteSquish(float value)
        {
            if (targetRenderer == null)
                return;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            ESCompositeURPProperties.SetInteractiveSquish(propertyBlock, value);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ResolveTarget()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();
        }
    }
}
