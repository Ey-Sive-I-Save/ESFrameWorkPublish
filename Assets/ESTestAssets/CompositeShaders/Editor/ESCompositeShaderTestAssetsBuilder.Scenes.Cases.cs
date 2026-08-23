using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ES.TestAssets.Editor
{
    internal static partial class ESCompositeShaderTestAssetsBuilder
    {
        private static readonly Color TestBackground = new Color(0.025f, 0.032f, 0.048f, 1f);

        private static void CreateOverviewScene(GeneratedTextures textures, GeneratedMaterials materials)
        {
            BuildScene(OverviewScenePath, "Composite Shader Test Overview", root =>
            {
                Camera camera = CreateOrthographicCamera(root, 6.7f, TestBackground);
                var animator = root.gameObject.AddComponent<ESCompositeShaderTestAnimator>();
                CreateSceneHeader(root, "Composite Shader 独立测试总览", "四类 Shader / 五个分类场景 / 一套验收路线", "先确认类别，再进入对应场景做动态观察", 5.85f, new Color(0.35f, 0.82f, 1f, 1f));
                CreateWorldLabel(
                    root,
                    "全部资产位于 Assets/ESTestAssets/CompositeShaders · 不引用 ESNormalAssets / SSU Demo",
                    new Vector3(0f, 4.28f, 0f),
                    0.045f,
                    new Color(0.48f, 0.82f, 1f, 1f));

                var landmarks = new List<Transform>();
                SpriteRenderer neutral = CreateSpriteCase(root, "00 · 原始无效果基准", new Vector3(-3.6f, -2.4f, 0f), textures.IconSprite, materials.Get("2d.base"), 2.2f);
                SpriteRenderer twoD = CreateSpriteCase(root, "01 · 2D 效果矩阵", new Vector3(-6f, 1.9f, 0f), textures.IconSprite, materials.Get("2d.enchanted"), 2f);
                Image ui = CreateWorldSpaceUICase(root, "02 · UI 交互表现", new Vector3(-2f, 1.9f, 0f), textures.IconSprite, materials.Get("ui.hologram"));
                Renderer lit = CreatePrimitiveCase(root, "03 · 3D Lit 受光效果", PrimitiveType.Sphere, new Vector3(2f, 1.8f, 0f), materials.Get("lit.metal_enchanted"), Vector3.one * 2.2f);
                Renderer vfx = CreatePrimitiveCase(root, "04 · 3D VFX 特效合同", PrimitiveType.Quad, new Vector3(6f, 1.9f, 0f), materials.Get("vfx.hologram"), Vector3.one * 2.5f);
                SpriteRenderer production = CreateSpriteCase(root, "05 · 生产配方与质量对比", new Vector3(3.6f, -2.4f, 0f), textures.IconSprite, materials.Get("prod.high"), 2.4f);
                animator.AddRendererCase(neutral, "00 · 原始无效果基准", materials.Get("2d.base"), "overview.neutral");
                animator.AddRendererCase(twoD, "01 · 2D 效果矩阵", materials.Get("2d.enchanted"), "overview.2d");
                animator.AddGraphicCase(ui, "02 · UI 交互表现", materials.Get("ui.hologram"), "overview.ui");
                animator.AddRendererCase(lit, "03 · 3D Lit 受光效果", materials.Get("lit.metal_enchanted"), "overview.lit");
                animator.AddRendererCase(vfx, "04 · 3D VFX 特效合同", materials.Get("vfx.hologram"), "overview.vfx");
                animator.AddRendererCase(production, "05 · 生产配方与质量对比", materials.Get("prod.high"), "overview.production");
                landmarks.Add(neutral.transform); landmarks.Add(twoD.transform); landmarks.Add(ui.transform); landmarks.Add(lit.transform); landmarks.Add(vfx.transform); landmarks.Add(production.transform);
                animator.AddRendererTrack(twoD, "_ShineIntensity", 0.35f, 1.8f, 0.8f);
                animator.AddRendererTrack(production, "_ShineIntensity", 0.25f, 1.8f, 1.1f, Mathf.PI);
                animator.AddRotatingTarget(lit.transform);
                CreateRuntimeTool(root, "Composite Shader 独立测试总览", "Overview", "分类入口、材质命名和运行态连接", animator);

                CreateLighting(root);
                CreateGuide(
                    root,
                    camera,
                    "Composite Shader 独立测试总览",
                    "分别打开 Scenes 下 01-05 场景。总览仅用于入口确认，逐项验收在分类场景完成。",
                    landmarks);
            });
        }

        private static void Create2DScene(GeneratedTextures textures, GeneratedMaterials materials)
        {
            BuildScene(SceneRoot + "/01_CompositeShader_2D_Cases.unity", "Composite Shader 2D Cases", root =>
            {
                Camera camera = CreateOrthographicCamera(root, 8.2f, TestBackground);
                var animator = root.gameObject.AddComponent<ESCompositeShaderTestAnimator>();
                CreateSceneHeader(root, "2D Composite Shader", "16 个案例 · Sprite / UV / 状态 / 轮廓", "扫光方向、溶解边缘、局部 UV、外描边裁剪", 7.6f, new Color(1f, 0.55f, 0.28f, 1f));
                string[] ids =
                {
                    "2d.base", "2d.shine_horizontal", "2d.shine_diagonal", "2d.dissolve_directional",
                    "2d.dissolve_radial", "2d.pixel_outline", "2d.hologram_local", "2d.glitch",
                    "2d.frozen", "2d.burn", "2d.poison", "2d.camouflage",
                    "2d.metal", "2d.enchanted", "2d.motion_squish", "2d.distortion_chromatic",
                };
                string[] labels =
                {
                    "Base · 无效果基准", "Shine 水平", "Shine 斜向", "方向发光溶解",
                    "源点扩散", "像素描边", "Hologram 局部UV", "Glitch 横向",
                    "冰冻", "燃烧", "中毒", "迷彩",
                    "流动金属", "附魔流光", "挤压 + 摆动", "UV扰动 + 色差",
                };

                var landmarks = new List<Transform>();
                for (int i = 0; i < ids.Length; i++)
                {
                    int column = i % 4;
                    int row = i / 4;
                    Vector3 position = new Vector3(-6.3f + column * 4.2f, 3.75f - row * 3.35f, 0f);
                    SpriteRenderer renderer = CreateSpriteCase(root, labels[i], position, textures.IconSprite, materials.Get(ids[i]), 1.45f, 1.58f, 0.042f);
                    animator.AddRendererCase(renderer, labels[i], materials.Get(ids[i]), ids[i]);
                    if (column == 0) landmarks.Add(renderer.transform);
                    if (ids[i] == "2d.shine_horizontal") animator.AddRendererTrack(renderer, "_ShineIntensity", 0.25f, 1.8f, 0.9f);
                    if (ids[i] == "2d.dissolve_directional") animator.AddRendererTrack(renderer, "_FadeProgress", 0.15f, 0.85f, 0.65f);
                    if (ids[i] == "2d.hologram_local") animator.AddRendererTrack(renderer, "_HologramLineFrequency", 42f, 92f, 0.75f);
                }
                CreateRuntimeTool(root, "2D Composite Shader", "2D / Sprite", "方向、UV、父开关和几何边界", animator);

                CreateGuide(
                    root,
                    camera,
                    "2D Composite Shader · 16 案例",
                    "重点检查扫光方向、局部 UV 全息、溶解边缘、外形裁剪与状态效果叠加。",
                    landmarks);
            });
        }

        private static void CreateUIScene(GeneratedTextures textures, GeneratedMaterials materials)
        {
            BuildScene(SceneRoot + "/02_CompositeShader_UI_Cases.unity", "Composite Shader UI Cases", root =>
            {
                Camera camera = CreateOrthographicCamera(root, 5f, TestBackground);
                GameObject canvasObject = new GameObject("UI Test Canvas");
                canvasObject.transform.SetParent(root, false);
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();

                CreateUIText(canvasObject.transform, "UI Composite Shader · 10 个实用案例", new Vector2(0f, 475f), new Vector2(1000f, 70f), 38, Color.white);
                CreateUIText(canvasObject.transform, "Canvas / Stencil / Mask 语义与普通 Renderer 分开验收", new Vector2(0f, 425f), new Vector2(1200f, 45f), 22, new Color(0.5f, 0.82f, 1f, 1f));

                string[] ids =
                {
                    "ui.base", "ui.shine_button", "ui.hologram", "ui.glitch", "ui.enchanted",
                    "ui.shifting", "ui.sine_glow", "ui.pixelate", "ui.dissolve", "ui.recolor_outline",
                };
                string[] labels =
                {
                    "Base · 无效果基准", "按钮扫光", "科技全息", "故障警告", "稀有附魔",
                    "传奇流变 · Stencil", "交互呼吸", "冷却像素化", "面板揭示", "主题换肤 · RectMask2D",
                };
                var landmarks = new List<Transform>();
                var animator = root.gameObject.AddComponent<ESCompositeShaderTestAnimator>();
                for (int i = 0; i < ids.Length; i++)
                {
                    int column = i % 5;
                    int row = i / 5;
                    Vector2 position = new Vector2(-680f + column * 340f, 205f - row * 390f);
                    bool useStencilMask = ids[i] == "ui.shifting";
                    bool useRectMask = ids[i] == "ui.recolor_outline";
                    Image image = CreateUICase(
                        canvasObject.transform,
                        labels[i],
                        position,
                        textures.IconSprite,
                        materials.Get(ids[i]),
                        useStencilMask,
                        useRectMask);
                    animator.AddGraphicCase(image, labels[i], materials.Get(ids[i]), ids[i]);
                    if (column == 0) landmarks.Add(image.transform);
                    if (ids[i] == "ui.dissolve") animator.AddGraphicTrack(image, "_FadeProgress", 0.18f, 0.82f, 0.7f);
                }
                CreateRuntimeTool(root, "UI Composite Shader", "UI / Canvas", "Stencil、RectMask2D、材质实例和动态揭示", animator);

                CreateGuide(
                    root,
                    camera,
                    "UI Composite Shader · 10 案例",
                    "重点检查 Canvas 材质实例、裁剪边界、UI 全息局部 UV 与动态溶解。",
                    landmarks,
                    false);
            });
        }

        private static void CreateLitScene(GeneratedMaterials materials)
        {
            BuildScene(SceneRoot + "/03_CompositeShader_3D_Lit_Cases.unity", "Composite Shader 3D Lit Cases", root =>
            {
                Camera camera = CreatePerspectiveCamera(root, new Vector3(0f, 8f, -19f), new Vector3(0f, 1.2f, 2.5f));
                CreateSceneHeader(root, "3D Lit Composite Shader", "10 个案例 · 受光材质 / 角色表现 / 状态效果", "Rim、扫光、溶解、全息和光照一致性", 7.4f, new Color(1f, 0.82f, 0.35f, 1f));
                CreateLighting(root);
                CreateFloor(root, materials.Get("env.dark"), new Vector3(0f, -1.2f, 2.5f), new Vector3(25f, 0.4f, 13f));
                string[] ids = { "lit.base", "lit.rim", "lit.shine", "lit.dissolve", "lit.hologram", "lit.glitch", "lit.frozen", "lit.burn", "lit.camouflage", "lit.metal_enchanted" };
                string[] labels = { "Base · 无效果基准", "角色 Rim", "拾取 Shine", "生成 Dissolve", "投影 Hologram", "受损 Glitch", "冰冻", "燃烧", "潜行迷彩", "附魔金属" };
                PrimitiveType[] primitives = { PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cube, PrimitiveType.Sphere };
                var landmarks = new List<Transform>();
                var animator = root.gameObject.AddComponent<ESCompositeShaderTestAnimator>();

                for (int i = 0; i < ids.Length; i++)
                {
                    int column = i % 5;
                    int row = i / 5;
                    Vector3 position = new Vector3(-7.2f + column * 3.6f, row == 0 ? 2.2f : 1.7f, row * 4.8f);
                    Vector3 scale = primitives[i] == PrimitiveType.Capsule ? new Vector3(1.5f, 1.8f, 1.5f) : Vector3.one * 2.25f;
                    Renderer renderer = CreatePrimitiveCase(root, labels[i], primitives[i], position, materials.Get(ids[i]), scale);
                    animator.AddRendererCase(renderer, labels[i], materials.Get(ids[i]), ids[i]);
                    if (column == 0) landmarks.Add(renderer.transform);
                    if (ids[i] == "lit.dissolve") animator.AddRendererTrack(renderer, "_DissolveProgress", 0.15f, 0.85f, 0.65f);
                    animator.AddRotatingTarget(renderer.transform);
                }
                CreateRuntimeTool(root, "3D Lit Composite Shader", "3D / Lit", "URP 受光、Rim、状态叠加和动态溶解", animator);

                CreateGuide(
                    root,
                    camera,
                    "3D Lit Composite Shader · 10 案例",
                    "检查 URP 受光基线、Rim、扫光、状态效果与光照结果是否同时成立。",
                    landmarks);
            });
        }

        private static void CreateVfxScene(GeneratedMaterials materials)
        {
            BuildScene(SceneRoot + "/04_CompositeShader_3D_VFX_Cases.unity", "Composite Shader 3D VFX Cases", root =>
            {
                Camera camera = CreatePerspectiveCamera(root, new Vector3(0f, 7.2f, -19f), new Vector3(0f, 1.6f, 4.1f));
                CreateSceneHeader(root, "3D VFX Composite Shader", "无效果基准 + 10 个案例 + 粒子顶点流", "序列帧、Polar、Flow、Custom1/2、深度和软粒子", 7.4f, new Color(0.72f, 0.45f, 1f, 1f));
                CreateLighting(root);
                CreateFloor(root, materials.Get("env.dark"), new Vector3(0f, -1.4f, 2.5f), new Vector3(25f, 0.35f, 13f));
                string[] ids = { "vfx.base", "vfx.sequence", "vfx.polar", "vfx.vertex_animation", "vfx.flow", "vfx.shine", "vfx.radial", "vfx.dissolve", "vfx.hologram", "vfx.glitch", "vfx.depth" };
                string[] labels = { "Base · 无效果基准", "4x4 序列帧", "极坐标旋涡", "顶点动画 · 能量飘带", "Flow Map · 定向流动", "技能轨迹扫光", "范围径向遮罩", "传送门消散", "数字全息", "信号故障", "深度交界 + 软粒子" };
                var landmarks = new List<Transform>();
                var animator = root.gameObject.AddComponent<ESCompositeShaderTestAnimator>();

                for (int i = 0; i < ids.Length; i++)
                {
                    int column = i % 6;
                    int row = i / 6;
                    Vector3 position = new Vector3(-8.75f + column * 3.5f, 2f, row * 4.5f);
                    bool isDepth = ids[i] == "vfx.depth";
                    Renderer renderer = CreatePrimitiveCase(root, labels[i], isDepth ? PrimitiveType.Sphere : PrimitiveType.Quad, position, materials.Get(ids[i]), isDepth ? Vector3.one * 2f : new Vector3(2.35f, 2.35f, 1f), 0.62f, 0.043f);
                    animator.AddRendererCase(renderer, labels[i], materials.Get(ids[i]), ids[i]);
                    if (column == 0) landmarks.Add(renderer.transform);
                    if (ids[i] == "vfx.dissolve") animator.AddRendererTrack(renderer, "_DissolveProgress", 0.15f, 0.85f, 0.72f);
                    if (ids[i] == "vfx.radial") animator.AddRendererTrack(renderer, "_RadialMaskRadius", 0.18f, 0.68f, 0.8f, 1f);
                    if (isDepth)
                    {
                        Renderer blocker = CreatePrimitiveCase(root, "Depth Reference", PrimitiveType.Cube, position + new Vector3(0.8f, 0f, 0.8f), materials.Get("env.neutral"), new Vector3(1.2f, 3f, 0.6f));
                        blocker.transform.SetSiblingIndex(renderer.transform.GetSiblingIndex());
                    }
                }

                CreateParticleVertexStreamCase(root, new Vector3(0f, 0.55f, 8.2f), materials.Get("vfx.vertex_animation"), animator);
                CreateRuntimeTool(root, "3D VFX Composite Shader", "3D / VFX", "序列帧、Flow、顶点流、深度交界和软粒子", animator);
                CreateGuide(
                    root,
                    camera,
                    "3D VFX Composite Shader · 无效果基准 + 10 配方 + 粒子顶点流",
                    "检查序列帧、极坐标、流图、方向参数、软粒子/深度交界与 Custom1/Custom2 顶点流。",
                    landmarks);
            });
        }

        private static void CreateProductionScene(GeneratedTextures textures, GeneratedMaterials materials)
        {
            BuildScene(SceneRoot + "/05_CompositeShader_ProductionRecipes.unity", "Composite Shader Production Recipes", root =>
            {
                Camera camera = CreateOrthographicCamera(root, 8f, TestBackground);
                CreateLighting(root);
                CreateSceneHeader(root, "Composite Shader 生产配方", "质量档 / 方向 / 顺序 / MPB · 面向落地的对照场", "Basic/Standard/High、方向参数、顺序和共享材质实例", 7.25f, new Color(0.35f, 1f, 0.72f, 1f));
                var landmarks = new List<Transform>();
                var animator = root.gameObject.AddComponent<ESCompositeShaderTestAnimator>();

                SpriteRenderer neutral = CreateSpriteCase(root, "Base · 无效果基准", new Vector3(-5.5f, -3.4f, 0f), textures.IconSprite, materials.Get("2d.base"), 1.65f);
                animator.AddRendererCase(neutral, "Base · 无效果基准", materials.Get("2d.base"), "prod.neutral");
                landmarks.Add(neutral.transform);

                string[] topIds = { "prod.basic", "prod.standard", "prod.high", "prod.dir_right", "prod.dir_up", "prod.dir_diag" };
                string[] topLabels = { "Quality Basic", "Quality Standard", "Quality High Exact", "Shine →", "Shine ↑", "Shine ↗" };
                for (int i = 0; i < topIds.Length; i++)
                {
                    SpriteRenderer renderer = CreateSpriteCase(root, topLabels[i], new Vector3(-7.5f + i * 3f, 3.75f, 0f), textures.IconSprite, materials.Get(topIds[i]), 1.35f, 1.62f, 0.042f);
                    animator.AddRendererCase(renderer, topLabels[i], materials.Get(topIds[i]), topIds[i]);
                    if (i == 0 || i == 3) landmarks.Add(renderer.transform);
                }

                SpriteRenderer orderUv = CreateSpriteCase(root, "顺序：UV → Color", new Vector3(-5.5f, 0.8f, 0f), textures.IconSprite, materials.Get("prod.order_color_uv"), 1.65f);
                animator.AddRendererCase(orderUv, "顺序：UV → Color", materials.Get("prod.order_color_uv"), "prod.order_color_uv");
                SpriteRenderer orderStatus = CreateSpriteCase(root, "顺序：Fade → Status", new Vector3(-1.8f, 0.8f, 0f), textures.IconSprite, materials.Get("prod.order_fade_status"), 1.65f);
                animator.AddRendererCase(orderStatus, "顺序：Fade → Status", materials.Get("prod.order_fade_status"), "prod.order_fade_status");
                animator.AddRendererTrack(orderStatus, "_FadeProgress", 0.15f, 0.85f, 0.65f);
                landmarks.Add(orderUv.transform);

                SpriteRenderer sharedA = CreateSpriteCase(root, "MPB 同材质 · A", new Vector3(2f, 0.8f, 0f), textures.IconSprite, materials.Get("prod.mpb_a"), 1.65f);
                SpriteRenderer sharedB = CreateSpriteCase(root, "MPB 同材质 · B", new Vector3(5.7f, 0.8f, 0f), textures.IconSprite, materials.Get("prod.mpb_a"), 1.65f);
                animator.AddRendererCase(sharedA, "MPB 同材质 · A", materials.Get("prod.mpb_a"), "prod.mpb_a");
                animator.AddRendererCase(sharedB, "MPB 同材质 · B", materials.Get("prod.mpb_a"), "prod.mpb_b");
                animator.AddRendererTrack(sharedA, "_ShineIntensity", 0.25f, 2f, 1.1f);
                animator.AddRendererTrack(sharedB, "_ShineIntensity", 0.25f, 2f, 1.1f, Mathf.PI);

                Renderer litMpb = CreatePrimitiveCase(root, "MPB Lit Rim", PrimitiveType.Sphere, new Vector3(1f, -3.4f, 0f), materials.Get("prod.mpb_b"), Vector3.one * 2f);
                animator.AddRendererCase(litMpb, "MPB Lit Rim", materials.Get("prod.mpb_b"), "prod.mpb_lit");
                animator.AddRendererTrack(litMpb, "_RimIntensity", 0.2f, 2.2f, 0.9f);
                animator.AddRotatingTarget(litMpb.transform);
                landmarks.Add(litMpb.transform);
                CreateRuntimeTool(root, "Composite Shader 生产配方", "Production Recipes", "质量档、方向、效果顺序和 MPB 实例差异", animator);

                CreateGuide(
                    root,
                    camera,
                    "Composite Shader 生产配方",
                    "先横向比较质量档，再核对三个扫光方向，最后验证效果顺序与同材质 MPB 实例差异。",
                    landmarks);
            });
        }

        private static Image CreateUICase(
            Transform parent,
            string title,
            Vector2 position,
            Sprite sprite,
            Material material,
            bool useStencilMask,
            bool useRectMask)
        {
            GameObject group = new GameObject("Case · " + title, typeof(RectTransform));
            RectTransform groupRect = (RectTransform)group.transform;
            groupRect.SetParent(parent, false);
            groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = position;
            groupRect.sizeDelta = new Vector2(290f, 300f);

            Transform imageParent = groupRect;
            if (useStencilMask || useRectMask)
            {
                GameObject clipObject = new GameObject(
                    useStencilMask ? "Stencil Mask" : "RectMask2D",
                    typeof(RectTransform));
                RectTransform clipRect = (RectTransform)clipObject.transform;
                clipRect.SetParent(groupRect, false);
                clipRect.anchorMin = clipRect.anchorMax = new Vector2(0.5f, 0.5f);
                clipRect.anchoredPosition = new Vector2(0f, 25f);
                clipRect.sizeDelta = new Vector2(170f, 170f);
                imageParent = clipRect;

                if (useStencilMask)
                {
                    Image maskImage = clipObject.AddComponent<Image>();
                    maskImage.sprite = sprite;
                    maskImage.color = Color.white;
                    maskImage.raycastTarget = false;
                    Mask mask = clipObject.AddComponent<Mask>();
                    mask.showMaskGraphic = false;
                }
                else
                {
                    clipObject.AddComponent<RectMask2D>();
                }
            }

            GameObject imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform imageRect = (RectTransform)imageObject.transform;
            imageRect.SetParent(imageParent, false);
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = useStencilMask || useRectMask ? new Vector2(48f, 0f) : new Vector2(0f, 25f);
            imageRect.sizeDelta = new Vector2(220f, 220f);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.material = material;
            image.preserveAspect = true;
            image.raycastTarget = false;

            CreateUIText(groupRect, FormatCaseLabel(title, material), new Vector2(0f, -160f), new Vector2(350f, 92f), 17, new Color(0.85f, 0.9f, 0.98f, 1f));
            return image;
        }

        private static Image CreateWorldSpaceUICase(Transform root, string title, Vector3 position, Sprite sprite, Material material)
        {
            GameObject caseRoot = new GameObject("Case · " + title);
            caseRoot.transform.SetParent(root, false);
            caseRoot.transform.localPosition = position;

            GameObject canvasObject = new GameObject("World Space UI Canvas", typeof(RectTransform), typeof(Canvas));
            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.SetParent(caseRoot.transform, false);
            canvasRect.sizeDelta = new Vector2(240f, 240f);
            canvasRect.localScale = Vector3.one * 0.012f;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            GameObject imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform imageRect = (RectTransform)imageObject.transform;
            imageRect.SetParent(canvasRect, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.sizeDelta = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.material = material;
            image.preserveAspect = true;
            image.raycastTarget = false;

            CreateWorldLabel(caseRoot.transform, title, new Vector3(0f, 1.58f, 0f), 0.058f);
            CreateWorldLabel(caseRoot.transform, "材质 · " + (material == null ? "<材质缺失>" : material.name), new Vector3(0f, 1.24f, 0f), 0.042f, new Color(0.64f, 0.72f, 0.84f, 1f));
            return image;
        }

        private static Text CreateUIText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            GameObject textObject = new GameObject(value + " Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, fontSize - 7);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static void CreateParticleVertexStreamCase(Transform root, Vector3 position, Material material, ESCompositeShaderTestAnimator animator)
        {
            GameObject caseRoot = new GameObject("Case · 粒子顶点流 · Custom1/Custom2");
            caseRoot.transform.SetParent(root, false);
            caseRoot.transform.localPosition = position;
            GameObject instance = new GameObject("粒子顶点流 · Custom1/Custom2");
            instance.transform.SetParent(caseRoot.transform, false);
            ParticleSystem particles = instance.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true; main.startLifetime = 2.5f; main.startSpeed = 0.45f; main.startSize = 0.75f; main.maxParticles = 32;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 8f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 1f;

            ParticleSystem.CustomDataModule custom = particles.customData;
            custom.enabled = true;
            custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
            custom.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(-0.08f, 0.08f));
            custom.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(-0.04f, 0.04f));
            custom.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(0f, 15f));
            custom.SetVector(ParticleSystemCustomData.Custom1, 3, new ParticleSystem.MinMaxCurve(0.15f, 0.9f));
            custom.SetMode(ParticleSystemCustomData.Custom2, ParticleSystemCustomDataMode.Vector);
            custom.SetVectorComponentCount(ParticleSystemCustomData.Custom2, 1);
            custom.SetVector(ParticleSystemCustomData.Custom2, 0, new ParticleSystem.MinMaxCurve(0.4f, 1.4f));

            ParticleSystemRenderer renderer = instance.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XYZW,
                ParticleSystemVertexStream.Custom2X,
            });
            CreateWorldLabel(caseRoot.transform, "粒子顶点流 · Custom1/Custom2", new Vector3(0f, 1.8f, 0f), 0.045f);
            CreateWorldLabel(caseRoot.transform, "材质 · " + (material == null ? "<材质缺失>" : material.name), new Vector3(0f, 1.46f, 0f), 0.038f, new Color(0.64f, 0.72f, 0.84f, 1f));
            animator.AddRendererCase(renderer, "粒子顶点流 · Custom1/Custom2", material, "vfx.vertex_streams");
        }
    }
}
