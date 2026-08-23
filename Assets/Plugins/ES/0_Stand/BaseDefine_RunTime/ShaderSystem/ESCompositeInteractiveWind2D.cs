using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 根据 2D 触发器接触位置驱动对象级风弯曲，不实例化或修改共享材质。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    [AddComponentMenu("【ES】/相机与表现/Composite Interactive Wind 2D")]
    public sealed class ESCompositeInteractiveWind2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private BoxCollider2D interactionArea;
        [SerializeField, Range(0f, 89f)] private float maximumRotation = 18f;
        [SerializeField, Min(0f)] private float bendInSpeed = 8f;
        [SerializeField, Min(0f)] private float bendOutSpeed = 8f;
        [SerializeField] private bool stayBent = true;
        [SerializeField, Min(0f)] private float minimumInteractionSpeed = 1f;
        [SerializeField, Min(0f)] private float bendHeightOverride;

        private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
        private MaterialPropertyBlock propertyBlock;
        private float currentRotation;
        private float interactionDirection;
        private float previousInteractionX;
        private bool hasPreviousInteractionX;

        private void Reset()
        {
            ResolveTargets();
            if (interactionArea != null)
                interactionArea.isTrigger = true;
        }

        private void OnEnable()
        {
            ResolveTargets();
            contacts.Clear();
            currentRotation = 0f;
            interactionDirection = 0f;
            hasPreviousInteractionX = false;
            WriteWind(0f);
        }

        private void OnDisable()
        {
            contacts.Clear();
            currentRotation = 0f;
            interactionDirection = 0f;
            hasPreviousInteractionX = false;
            WriteWind(0f);
        }

        private void OnValidate()
        {
            maximumRotation = Mathf.Clamp(maximumRotation, 0f, 89f);
            bendInSpeed = Mathf.Max(0f, bendInSpeed);
            bendOutSpeed = Mathf.Max(0f, bendOutSpeed);
            minimumInteractionSpeed = Mathf.Max(0f, minimumInteractionSpeed);
            bendHeightOverride = Mathf.Max(0f, bendHeightOverride);
            ResolveTargets();
        }

        private void FixedUpdate()
        {
            contacts.RemoveWhere(contact => contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy);

            float targetRotation = 0f;
            bool hasContact = TryGetStrongestContact(out float localX, out float penetration);
            if (hasContact)
            {
                if (Mathf.Abs(interactionDirection) < 0.5f)
                    interactionDirection = localX < 0f ? 1f : -1f;
                targetRotation = interactionDirection * maximumRotation * penetration;

                if (!stayBent)
                {
                    float speed = hasPreviousInteractionX
                        ? Mathf.Abs(localX - previousInteractionX) / Mathf.Max(Time.fixedDeltaTime, 0.0001f)
                        : minimumInteractionSpeed;
                    if (speed < minimumInteractionSpeed)
                        targetRotation = 0f;
                }

                previousInteractionX = localX;
                hasPreviousInteractionX = true;
            }
            else
            {
                hasPreviousInteractionX = false;
            }

            float response = Mathf.Abs(targetRotation) > 0.0001f ? bendInSpeed : bendOutSpeed;
            float blend = response <= 0f ? 1f : 1f - Mathf.Exp(-response * Time.fixedDeltaTime);
            float nextRotation = Mathf.Lerp(currentRotation, targetRotation, blend);
            if (Mathf.Abs(nextRotation) < 0.0001f)
                nextRotation = 0f;
            if (Mathf.Approximately(nextRotation, currentRotation))
                return;

            currentRotation = nextRotation;
            WriteWind(currentRotation);
            if (!hasContact && currentRotation == 0f)
                interactionDirection = 0f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;
            if (contacts.Count == 0 || Mathf.Abs(currentRotation) < maximumRotation * 0.05f)
                interactionDirection = 0f;
            contacts.Add(other);
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

        private bool TryGetStrongestContact(out float strongestX, out float strongestPenetration)
        {
            strongestX = 0f;
            strongestPenetration = 0f;
            if (interactionArea == null)
                return false;

            float halfWidth = Mathf.Max(Mathf.Abs(interactionArea.size.x) * 0.5f, 0.0001f);
            foreach (Collider2D contact in contacts)
            {
                if (contact == null)
                    continue;

                float localX = transform.InverseTransformPoint(contact.bounds.center).x - interactionArea.offset.x;
                float penetration = Mathf.Clamp01((halfWidth - Mathf.Abs(localX)) / halfWidth);
                if (penetration <= strongestPenetration)
                    continue;

                strongestX = localX;
                strongestPenetration = penetration;
            }

            return strongestPenetration > 0f;
        }

        private void WriteWind(float rotation)
        {
            if (targetRenderer == null)
                return;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            ESCompositeURPProperties.SetInteractiveWind(propertyBlock, rotation, ResolveBendHeight());
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private float ResolveBendHeight()
        {
            if (bendHeightOverride > 0.0001f)
                return bendHeightOverride;
            if (targetRenderer != null && targetRenderer.sprite != null)
                return Mathf.Max(targetRenderer.sprite.bounds.size.y, 0.0001f);
            return interactionArea != null
                ? Mathf.Max(Mathf.Abs(interactionArea.size.y), 0.0001f)
                : 1f;
        }

        private void ResolveTargets()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();
            if (interactionArea == null)
                interactionArea = GetComponent<BoxCollider2D>();
        }
    }
}
