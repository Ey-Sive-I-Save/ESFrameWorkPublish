using UnityEngine;
using Newtonsoft.Json;
using System;

namespace ES.VMCP
{
    /// <summary>
    /// 通用组件配置操作类型
    /// </summary>
    public enum CommonComponentConfigOperation
    {
        ConfigureCollider,    // 配置碰撞器
        ConfigureRigidbody,   // 配置刚体
        ConfigureCamera,      // 配置相机
        ConfigureLight,       // 配置光源（快捷方式）
        ConfigureAudioSource, // 配置音频源
        ConfigureParticleSystem // 配置粒子系统
    }

    /// <summary>
    /// 统一的组件快捷配置命令
    /// </summary>
    [ESVMCPCommand("CommonComponentConfigOperation", "统一的组件快捷配置命令")]
    public class ComponentConfigOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonComponentConfigOperation Operation { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        // Collider配置
        [JsonProperty("isTrigger")]
        public bool? IsTrigger { get; set; }

        [JsonProperty("center")]
        [JsonConverter(typeof(Vector3Converter))]
        public Vector3? Center { get; set; }

        [JsonProperty("size")]
        [JsonConverter(typeof(Vector3Converter))]
        public Vector3? Size { get; set; }

        [JsonProperty("radius")]
        public float? Radius { get; set; }

        [JsonProperty("height")]
        public float? Height { get; set; }

        // Rigidbody配置
        [JsonProperty("mass")]
        public float? Mass { get; set; }

        [JsonProperty("drag")]
        public float? Drag { get; set; }

        [JsonProperty("angularDrag")]
        public float? AngularDrag { get; set; }

        [JsonProperty("useGravity")]
        public bool? UseGravity { get; set; }

        [JsonProperty("isKinematic")]
        public bool? IsKinematic { get; set; }

        // Camera配置
        [JsonProperty("fieldOfView")]
        public float? FieldOfView { get; set; }

        [JsonProperty("nearClipPlane")]
        public float? NearClipPlane { get; set; }

        [JsonProperty("farClipPlane")]
        public float? FarClipPlane { get; set; }

        [JsonProperty("orthographic")]
        public bool? Orthographic { get; set; }

        [JsonProperty("orthographicSize")]
        public float? OrthographicSize { get; set; }

        // AudioSource配置
        [JsonProperty("volume")]
        public float? Volume { get; set; }

        [JsonProperty("pitch")]
        public float? Pitch { get; set; }

        [JsonProperty("loop")]
        public bool? Loop { get; set; }

        [JsonProperty("playOnAwake")]
        public bool? PlayOnAwake { get; set; }

        public override string Description => $"配置组件: {Target} -> {Operation}";

        public override bool IsDangerous => false;

        public override ESVMCPValidationResult Validate()
        {
            if (string.IsNullOrEmpty(Target))
            {
                return ESVMCPValidationResult.Failure("target参数不能为空");
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                GameObject targetObj = TargetResolver.Resolve(Target, context);
                if (targetObj == null)
                {
                    return ESVMCPCommandResult.Failed($"找不到目标对象: {Target}");
                }

                switch (Operation)
                {
                    case CommonComponentConfigOperation.ConfigureCollider:
                        return ConfigureCollider(targetObj);

                    case CommonComponentConfigOperation.ConfigureRigidbody:
                        return ConfigureRigidbody(targetObj);

                    case CommonComponentConfigOperation.ConfigureCamera:
                        return ConfigureCamera(targetObj);

                    case CommonComponentConfigOperation.ConfigureAudioSource:
                        return ConfigureAudioSource(targetObj);

                    default:
                        return ESVMCPCommandResult.Failed($"未知的配置操作: {Operation}");
                }
            }
            catch (Exception ex)
            {
                return ESVMCPCommandResult.Failed($"配置组件失败: {ex.Message}");
            }
        }

        private ESVMCPCommandResult ConfigureCollider(GameObject obj)
        {
            // 尝试获取各种类型的碰撞器
            BoxCollider boxCollider = obj.GetComponent<BoxCollider>();
            SphereCollider sphereCollider = obj.GetComponent<SphereCollider>();
            CapsuleCollider capsuleCollider = obj.GetComponent<CapsuleCollider>();
            Collider collider = obj.GetComponent<Collider>();

            if (collider == null)
            {
                // 如果没有碰撞器，默认添加BoxCollider
                boxCollider = obj.AddComponent<BoxCollider>();
                Debug.Log($"[ESVMCP] 为对象 {Target} 添加了BoxCollider");
            }

            // 配置通用属性
            if (IsTrigger.HasValue && collider != null)
            {
                collider.isTrigger = IsTrigger.Value;
            }

            // 配置BoxCollider特有属性
            if (boxCollider != null)
            {
                if (Center.HasValue) boxCollider.center = Center.Value;
                if (Size.HasValue) boxCollider.size = Size.Value;
            }

            // 配置SphereCollider特有属性
            if (sphereCollider != null)
            {
                if (Center.HasValue) sphereCollider.center = Center.Value;
                if (Radius.HasValue) sphereCollider.radius = Radius.Value;
            }

            // 配置CapsuleCollider特有属性
            if (capsuleCollider != null)
            {
                if (Center.HasValue) capsuleCollider.center = Center.Value;
                if (Radius.HasValue) capsuleCollider.radius = Radius.Value;
                if (Height.HasValue) capsuleCollider.height = Height.Value;
            }

            Debug.Log($"[ESVMCP] 配置碰撞器: {Target}");
            return ESVMCPCommandResult.Succeed("碰撞器配置成功");
        }

        private ESVMCPCommandResult ConfigureRigidbody(GameObject obj)
        {
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = obj.AddComponent<Rigidbody>();
                Debug.Log($"[ESVMCP] 为对象 {Target} 添加了Rigidbody");
            }

            if (Mass.HasValue) rb.mass = Mass.Value;
            if (Drag.HasValue) rb.drag = Drag.Value;
            if (AngularDrag.HasValue) rb.angularDrag = AngularDrag.Value;
            if (UseGravity.HasValue) rb.useGravity = UseGravity.Value;
            if (IsKinematic.HasValue) rb.isKinematic = IsKinematic.Value;

            Debug.Log($"[ESVMCP] 配置刚体: {Target}");
            return ESVMCPCommandResult.Succeed("刚体配置成功");
        }

        private ESVMCPCommandResult ConfigureCamera(GameObject obj)
        {
            Camera cam = obj.GetComponent<Camera>();
            if (cam == null)
            {
                cam = obj.AddComponent<Camera>();
                Debug.Log($"[ESVMCP] 为对象 {Target} 添加了Camera");
            }

            if (FieldOfView.HasValue) cam.fieldOfView = FieldOfView.Value;
            if (NearClipPlane.HasValue) cam.nearClipPlane = NearClipPlane.Value;
            if (FarClipPlane.HasValue) cam.farClipPlane = FarClipPlane.Value;
            if (Orthographic.HasValue) cam.orthographic = Orthographic.Value;
            if (OrthographicSize.HasValue) cam.orthographicSize = OrthographicSize.Value;

            Debug.Log($"[ESVMCP] 配置相机: {Target}");
            return ESVMCPCommandResult.Succeed("相机配置成功");
        }

        private ESVMCPCommandResult ConfigureAudioSource(GameObject obj)
        {
            AudioSource audio = obj.GetComponent<AudioSource>();
            if (audio == null)
            {
                audio = obj.AddComponent<AudioSource>();
                Debug.Log($"[ESVMCP] 为对象 {Target} 添加了AudioSource");
            }

            if (Volume.HasValue) audio.volume = Volume.Value;
            if (Pitch.HasValue) audio.pitch = Pitch.Value;
            if (Loop.HasValue) audio.loop = Loop.Value;
            if (PlayOnAwake.HasValue) audio.playOnAwake = PlayOnAwake.Value;

            Debug.Log($"[ESVMCP] 配置音频源: {Target}");
            return ESVMCPCommandResult.Succeed("音频源配置成功");
        }
    }
}
