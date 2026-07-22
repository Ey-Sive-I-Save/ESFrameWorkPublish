using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Text;
namespace ES.Obsolete{
        /// <summary>
        /// ES文档窗口 - 专门用于制作文档界面的窗口
        /// 支持表格、代码块、网址、图片等高级特性
        /// </summary>
        public class ESDocWindow : ESMenuTreeWindowAB<ESDocWindow>
        {
            [MenuItem(MenuItemPathDefine.EDITOR_DOCS_PATH + "ES文档创建窗口")]
            public static void TryOpenWindow()
            {
                OpenWindow();
            }

            #region 简单重写
            public override GUIContent ESWindow_GetWindowGUIContent()
            {
                var content = new GUIContent("ES文档中心", "创建和编辑项目文档");
                return content;
            }

            public override void ESWindow_OnOpen()
            {
                base.ESWindow_OnOpen();
                if (UsingWindow.HasDelegate)
                {
                    //已经注册委托
                }
                else
                {
                    UsingWindow.DelegateHandle();
                }
            }

            private void DelegateHandle()
            {
                HasDelegate = true;
            }
            #endregion

            #region 数据滞留与声明
            public const string PageName_DocumentHome = "文档首页";
            public const string PageName_CreateDocument = "创建文档";
            public const string PageName_DocumentTemplates = "文档模板";
            public const string PageName_DocumentLibrary = "文档库";

            [NonSerialized] public Page_DocumentHome pageDocumentHome;
            [NonSerialized] public Page_CreateDocument pageCreateDocument;
            [NonSerialized] public Page_DocumentTemplates pageDocumentTemplates;
            [NonSerialized] public Page_ContentElementsReference pageContentReference;

            [NonSerialized] public List<ESDocumentPageBase> documentPages = new List<ESDocumentPageBase>();

            private bool HasDelegate = false;
            #endregion

            #region 缓冲刷新和加载保存
            protected override void OnImGUI()
            {
                if (UsingWindow == null)
                {
                    UsingWindow = this;
                    ES_LoadData();
                }
                base.OnImGUI();
            }

            public override void ESWindow_RefreshWindow()
            {
                base.ESWindow_RefreshWindow();
                ES_SaveData();
            }

            public override void ES_LoadData()
            {
                // 加载所有文档页面
                LoadAllDocuments();
            }

            public override void ES_SaveData()
            {
                // 保存数据逻辑
            }

            private void LoadAllDocuments()
            {
                documentPages.Clear();

                // 从指定路径加载所有文档SO资产
                string docPath = "Assets/Plugins/ES/Obsolete/Assets_ES_Legacy/Documentation";
                if (AssetDatabase.IsValidFolder(docPath))
                {
                    var guids = AssetDatabase.FindAssets("t:ESDocumentPageBase", new[] { docPath });
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var doc = AssetDatabase.LoadAssetAtPath<ESDocumentPageBase>(path);
                        if (doc != null)
                        {
                            documentPages.Add(doc);
                        }
                    }
                }
            }
            #endregion

            protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
            {
                base.ES_OnBuildMenuTree(tree);

                Part_BuildDocumentHome(tree);
                Part_BuildCreateDocument(tree);
                Part_BuildDocumentTemplates(tree);
                Part_BuildContentReference(tree);
                Part_BuildDocumentLibrary(tree);

                ES_LoadData();
            }

            #region 页面构建方法
            private void Part_BuildDocumentHome(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_DocumentHome, ref pageDocumentHome, SdfIconType.HouseDoor);
            }

            private void Part_BuildCreateDocument(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_CreateDocument, ref pageCreateDocument, SdfIconType.FilePlus);
            }

            private void Part_BuildDocumentTemplates(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_DocumentTemplates, ref pageDocumentTemplates, SdfIconType.FileEarmarkText);
            }

            private void Part_BuildContentReference(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, "内容元素参考", ref pageContentReference, SdfIconType.Book);
            }

            private void Part_BuildDocumentLibrary(OdinMenuTree tree)
            {
                // 动态添加所有文档页面
                foreach (var doc in documentPages)
                {
                    string category = string.IsNullOrEmpty(doc.category) ? "未分类" : doc.category;
                    var item = tree.Add($"{PageName_DocumentLibrary}/{category}/{doc.documentTitle}", doc, SdfIconType.FileEarmarkRichtext).First();
                    // 当在菜单中选择文档时，同步到Unity Selection以触发Inspector/Drawer预览
                    // item.OnSelect += (o) =>
                    // {
                    //     Selection.activeObject = doc;
                    // };
                }
            }
            #endregion
        }

        #region 文档首页
        [Serializable]
        public class Page_DocumentHome : ESWindowPageBase
        {
            [Title("ES文档中心", "项目文档管理系统", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string welcome = "欢迎使用ES文档中心\n\n在这里您可以:\n• 创建项目文档\n• 管理文档模板\n• 浏览文档库\n• 导出文档为Markdown";

            [Button("开始创建文档", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void GoToCreate()
            {
                if (ESDocWindow.MenuItems.TryGetValue(ESDocWindow.PageName_CreateDocument, out var item))
                {
                    ESDocWindow.UsingWindow.MenuTree.Selection.Add(item);
                }
            }

            [Button("浏览文档库", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void GoToLibrary()
            {
                if (ESDocWindow.MenuItems.TryGetValue(ESDocWindow.PageName_DocumentLibrary, out var item))
                {
                    ESDocWindow.UsingWindow.MenuTree.Selection.Add(item);
                }
            }

            [Title("演示文档")]
            [Button("📚 生成 Navmesh 使用指南演示", ButtonHeight = 45), GUIColor(0.4f, 0.7f, 0.9f)]
            public void CreateNavmeshDemoDocument()
            {
                var doc = ScriptableObject.CreateInstance<ESDocumentPageBase>();
                doc.documentTitle = "Unity Navmesh导航系统完整指南";
                doc.category = "教程";
                doc.author = "ES系统";
                doc.createDate = DateTime.Now.ToString("yyyy-MM-dd");
                doc.lastModified = DateTime.Now.ToString("yyyy-MM-dd");

                // ========== 第一章：概述 ==========
                var overview = new ESDocSection { sectionTitle = "📖 什么是Navmesh导航系统" };
                overview.content.Add(new ESDocText
                {
                    content = "NavMesh（Navigation Mesh，导航网格）是Unity中用于AI角色寻路的强大系统。它能够自动生成可行走区域的网格，并提供高效的路径规划算法，让游戏角色能够智能地在场景中移动、避障和导航。"
                });
                overview.content.Add(new ESDocAlert
                {
                    alertType = ESDocAlert.AlertType.Info,
                    title = "核心特性",
                    content = "✓ 自动生成导航网格\n✓ 动态障碍物支持\n✓ 多Agent类型配置\n✓ Off-Mesh Link跳跃/传送\n✓ 区域成本权重系统"
                });
                doc.sections.Add(overview);

                // ========== 第二章：环境准备 ==========
                var setup = new ESDocSection { sectionTitle = "🔧 环境设置与准备工作" };
                setup.content.Add(new ESDocText { content = "在开始使用Navmesh之前，需要完成以下准备步骤：" });
                
                var setupSteps = new ESDocOrderedList();
                setupSteps.items = new List<string>
                {
                    "确保场景中有地面物体（Plane、Terrain等）",
                    "选中地面物体，在Inspector中勾选 Navigation Static",
                    "打开导航窗口：Window → AI → Navigation",
                    "在Bake选项卡中配置参数",
                    "点击 Bake 按钮生成导航网格"
                };
                setup.content.Add(setupSteps);

                setup.content.Add(new ESDocAlert
                {
                    alertType = ESDocAlert.AlertType.Warning,
                    title = "注意事项",
                    content = "只有标记为 Navigation Static 的物体才会参与导航网格的烘焙！"
                });
                doc.sections.Add(setup);

                // ========== 第三章：Bake参数详解 ==========
                var bakeParams = new ESDocSection { sectionTitle = "⚙️ Bake参数配置详解" };
                
                var paramsTable = new ESDocTable { tableTitle = "Navmesh烘焙核心参数" };
                paramsTable.headers = new List<string> { "参数名称", "默认值", "作用说明", "调优建议" };
                paramsTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "Agent Radius", "0.5", "角色半径，影响可通过区域宽度", "设置为角色碰撞体半径" }
                });
                paramsTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "Agent Height", "2.0", "角色高度，影响可通过区域高度", "设置为角色碰撞体高度" }
                });
                paramsTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "Max Slope", "45", "可攀爬的最大坡度角度", "根据游戏设计调整" }
                });
                paramsTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "Step Height", "0.4", "可跨越的最大台阶高度", "小于此值的障碍会被忽略" }
                });
                paramsTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "Drop Height", "0", "允许掉落的最大高度", "0表示不允许掉落" }
                });
                bakeParams.content.Add(paramsTable);
                
                bakeParams.content.Add(new ESDocDivider());
                bakeParams.content.Add(new ESDocImage { caption = "Navmesh Bake面板示意图（在Navigation窗口中配置）" });
                doc.sections.Add(bakeParams);

                // ========== 第四章：NavMeshAgent组件 ==========
                var agentComponent = new ESDocSection { sectionTitle = "🤖 NavMeshAgent 组件使用" };
                agentComponent.content.Add(new ESDocText
                {
                    content = "NavMeshAgent是附加到游戏对象上的组件，负责实际的寻路和移动控制。"
                });

                agentComponent.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = @"using UnityEngine;
using UnityEngine.AI;

public class SimpleNavAgent : MonoBehaviour
{
    private NavMeshAgent agent;

    void Start()
    {
        // 获取NavMeshAgent组件
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // 鼠标点击移动示例
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // 设置目标位置
                agent.SetDestination(hit.point);
            }
        }
    }
}"
                });

                var agentPropsTable = new ESDocTable { tableTitle = "NavMeshAgent常用属性" };
                agentPropsTable.headers = new List<string> { "属性", "类型", "说明" };
                agentPropsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "speed", "float", "移动速度" } });
                agentPropsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "angularSpeed", "float", "旋转速度" } });
                agentPropsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "acceleration", "float", "加速度" } });
                agentPropsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "stoppingDistance", "float", "停止距离" } });
                agentPropsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "autoBraking", "bool", "自动刹车" } });
                agentPropsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "obstacleAvoidanceType", "enum", "障碍躲避质量等级" } });
                agentComponent.content.Add(agentPropsTable);
                doc.sections.Add(agentComponent);

                // ========== 第五章：常用API ==========
                var apiSection = new ESDocSection { sectionTitle = "📚 常用API方法详解" };
                
                apiSection.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = @"// ========== 移动控制 ==========

// 1. 设置目标位置（最常用）
agent.SetDestination(targetPosition);

// 2. 检查是否到达目的地
if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
{
    Debug.Log(""已到达目的地"");
}

// 3. 停止移动
agent.isStopped = true;  // 暂停
agent.ResetPath();       // 清除路径

// 4. 设置速度
agent.speed = 5.0f;

// ========== 路径查询 ==========

// 计算路径但不移动
NavMeshPath path = new NavMeshPath();
agent.CalculatePath(targetPos, path);

if (path.status == NavMeshPathStatus.PathComplete)
{
    Debug.Log(""找到完整路径"");
}

// ========== 传送 ==========

// 禁用agent后传送（避免错误）
agent.enabled = false;
transform.position = newPosition;
agent.enabled = true;

// 或使用Warp（推荐）
agent.Warp(newPosition);"
                });

                var methodsTable = new ESDocTable { tableTitle = "核心方法速查表" };
                methodsTable.headers = new List<string> { "方法", "返回值", "功能描述" };
                methodsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "SetDestination(Vector3)", "bool", "设置目标点并开始寻路" } });
                methodsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "CalculatePath(Vector3, NavMeshPath)", "bool", "计算路径但不移动" } });
                methodsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "Warp(Vector3)", "bool", "瞬移到指定位置" } });
                methodsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "SamplePathPosition(int, float, out NavMeshHit)", "bool", "在路径上采样位置" } });
                methodsTable.rows.Add(new ESDocTableRow { cells = new List<string> { "ResetPath()", "void", "清除当前路径" } });
                apiSection.content.Add(methodsTable);
                doc.sections.Add(apiSection);

                // ========== 第六章：巡逻系统实现 ==========
                var patrolSection = new ESDocSection { sectionTitle = "🚶 实战案例：AI巡逻系统" };
                patrolSection.content.Add(new ESDocText
                {
                    content = "下面演示一个完整的AI巡逻系统，包含多点循环巡逻、等待时间、到达检测等功能。"
                });

                patrolSection.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = @"using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class AIPatrolSystem : MonoBehaviour
{
    [Header(""巡逻点设置"")]
    public Transform[] patrolPoints;     // 巡逻点数组
    public bool loopPatrol = true;       // 是否循环巡逻
    
    [Header(""行为参数"")]
    public float waitTimeAtPoint = 2f;   // 每个点的等待时间
    public float detectionRadius = 10f;  // 检测半径
    
    private NavMeshAgent agent;
    private int currentPointIndex = 0;
    private bool isWaiting = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        if (patrolPoints.Length > 0)
        {
            GoToNextPoint();
        }
    }

    void Update()
    {
        // 检查是否到达当前巡逻点
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                if (!isWaiting)
                {
                    StartCoroutine(WaitAndContinue());
                }
            }
        }
    }

    IEnumerator WaitAndContinue()
    {
        isWaiting = true;
        
        // 在巡逻点等待
        yield return new WaitForSeconds(waitTimeAtPoint);
        
        GoToNextPoint();
        isWaiting = false;
    }

    void GoToNextPoint()
    {
        if (patrolPoints.Length == 0) return;

        // 设置下一个目标点
        agent.SetDestination(patrolPoints[currentPointIndex].position);
        
        // 更新索引
        currentPointIndex++;
        
        if (currentPointIndex >= patrolPoints.Length)
        {
            if (loopPatrol)
            {
                currentPointIndex = 0;  // 循环
            }
            else
            {
                currentPointIndex = patrolPoints.Length - 1;  // 停留在最后一点
            }
        }
    }

    // 调试绘制巡逻路径
    void OnDrawGizmosSelected()
    {
        if (patrolPoints == null || patrolPoints.Length < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < patrolPoints.Length - 1; i++)
        {
            if (patrolPoints[i] != null && patrolPoints[i + 1] != null)
            {
                Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
            }
        }

        // 如果是循环巡逻，连接首尾
        if (loopPatrol && patrolPoints[0] != null && patrolPoints[patrolPoints.Length - 1] != null)
        {
            Gizmos.DrawLine(patrolPoints[patrolPoints.Length - 1].position, patrolPoints[0].position);
        }
    }
}"
                });
                doc.sections.Add(patrolSection);

                // ========== 第七章：动态障碍物 ==========
                var obstacleSection = new ESDocSection { sectionTitle = "🚧 动态障碍物与NavMeshObstacle" };
                obstacleSection.content.Add(new ESDocText
                {
                    content = "NavMeshObstacle组件用于创建动态障碍物，可以在运行时移动，并实时影响导航。"
                });

                obstacleSection.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = @"using UnityEngine;
using UnityEngine.AI;

public class DynamicObstacle : MonoBehaviour
{
    private NavMeshObstacle obstacle;

    void Start()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        
        // 配置障碍物
        obstacle.carving = true;              // 启用雕刻（会实时修改navmesh）
        obstacle.carveOnlyStationary = true;  // 仅在静止时雕刻（性能优化）
        obstacle.carvingMoveThreshold = 0.1f; // 移动阈值
        obstacle.carvingTimeToStationary = 0.5f; // 多久算静止
    }

    // 示例：移动障碍物
    public void MoveObstacle(Vector3 targetPos)
    {
        // 可以直接移动带NavMeshObstacle的物体
        transform.position = targetPos;
        // NavMesh会自动更新
    }
}"
                });

                var obstacleTable = new ESDocTable { tableTitle = "NavMeshObstacle关键参数" };
                obstacleTable.headers = new List<string> { "参数", "说明", "性能影响" };
                obstacleTable.rows.Add(new ESDocTableRow { cells = new List<string> { "carving", "是否雕刻导航网格", "中等" } });
                obstacleTable.rows.Add(new ESDocTableRow { cells = new List<string> { "carveOnlyStationary", "仅静止时雕刻", "优化性能" } });
                obstacleTable.rows.Add(new ESDocTableRow { cells = new List<string> { "shape", "形状（盒体/胶囊体）", "低" } });
                obstacleSection.content.Add(obstacleTable);

                obstacleSection.content.Add(new ESDocAlert
                {
                    alertType = ESDocAlert.AlertType.Warning,
                    title = "性能警告",
                    content = "大量启用carving的NavMeshObstacle会影响性能！建议只在必要的移动障碍物上启用。"
                });
                doc.sections.Add(obstacleSection);

                // ========== 第八章：Off-Mesh Link ==========
                var offMeshSection = new ESDocSection { sectionTitle = "🌉 Off-Mesh Link（跳跃/传送点）" };
                offMeshSection.content.Add(new ESDocText
                {
                    content = "Off-Mesh Link用于连接不相连的导航区域，比如跳跃、爬梯、传送等特殊移动。"
                });

                var linkSteps = new ESDocOrderedList();
                linkSteps.items = new List<string>
                {
                    "创建两个空物体作为起点和终点",
                    "添加 Off Mesh Link 组件",
                    "设置 Start 和 End 为两个空物体",
                    "配置 Cost Modifier（路径成本）",
                    "勾选 Bi Directional（双向）如需要"
                };
                offMeshSection.content.Add(linkSteps);

                offMeshSection.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = @"using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class JumpController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 检测是否在Off-Mesh Link上
        if (agent.isOnOffMeshLink)
        {
            StartCoroutine(HandleOffMeshLink());
        }
    }

    IEnumerator HandleOffMeshLink()
    {
        // 获取Off-Mesh Link数据
        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPos = linkData.startPos;
        Vector3 endPos = linkData.endPos;

        // 播放跳跃动画
        if (animator != null)
        {
            animator.SetTrigger(""Jump"");
        }

        // 暂停自动移动
        agent.isStopped = true;

        // 手动移动到终点（可以用曲线、抛物线等）
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            // 抛物线跳跃
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 2f; // 跳跃高度
            
            transform.position = currentPos;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;

        // 完成跳跃，恢复自动移动
        agent.CompleteOffMeshLink();
        agent.isStopped = false;
    }
}"
                });
                doc.sections.Add(offMeshSection);

                // ========== 第九章：常见问题 ==========
                var faqSection = new ESDocSection { sectionTitle = "❓ 常见问题与解决方案" };

                faqSection.content.Add(new ESDocQuote
                {
                    quoteText = "问题1：Agent不移动或者移动异常？",
                    source = "FAQ"
                });
                var solution1 = new ESDocUnorderedList();
                solution1.items = new List<string>
                {
                    "检查是否烘焙了NavMesh（场景中应该能看到蓝色网格）",
                    "确认Agent起始位置在NavMesh上",
                    "检查Agent的isStopped是否为true",
                    "确认目标点在有效的NavMesh区域内"
                };
                faqSection.content.Add(solution1);

                faqSection.content.Add(new ESDocDivider());

                faqSection.content.Add(new ESDocQuote
                {
                    quoteText = "问题2：Agent穿模或者卡在墙里？",
                    source = "FAQ"
                });
                var solution2 = new ESDocUnorderedList();
                solution2.items = new List<string>
                {
                    "增大Agent Radius参数",
                    "检查Carve参数是否正确设置",
                    "确认障碍物有碰撞体且标记为Static"
                };
                faqSection.content.Add(solution2);

                faqSection.content.Add(new ESDocDivider());

                faqSection.content.Add(new ESDocAlert
                {
                    alertType = ESDocAlert.AlertType.Error,
                    title = "重要：传送Agent时的正确做法",
                    content = "永远不要直接修改transform.position！\n应该使用：agent.Warp(newPosition) 或先禁用agent再移动。"
                });
                doc.sections.Add(faqSection);

                // ========== 第十章：性能优化 ==========
                var perfSection = new ESDocSection { sectionTitle = "⚡ 性能优化技巧" };
                
                var perfTips = new ESDocOrderedList();
                perfTips.items = new List<string>
                {
                    "减少NavMesh烘焙精度（增大Cell Size）",
                    "使用agent.updateRotation控制是否自动旋转",
                    "降低obstacleAvoidanceType质量等级",
                    "减少carving的NavMeshObstacle数量",
                    "使用agent.updatePosition = false手动控制位置更新",
                    "大场景分区域烘焙NavMesh",
                    "使用NavMesh.SamplePosition优化目标点查询"
                };
                perfSection.content.Add(perfTips);

                perfSection.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = @"// 性能优化示例代码

// 1. 手动控制更新频率
void FixedUpdate()
{
    // 每0.2秒更新一次路径
    if (Time.time - lastUpdateTime > 0.2f)
    {
        agent.SetDestination(target.position);
        lastUpdateTime = Time.time;
    }
}

// 2. 验证目标点是否在NavMesh上
NavMeshHit hit;
if (NavMesh.SamplePosition(targetPos, out hit, 5f, NavMesh.AllAreas))
{
    agent.SetDestination(hit.position); // 使用修正后的位置
}

// 3. 批量禁用不活动的Agent
foreach (var agent in inactiveAgents)
{
    agent.enabled = false;
}"
                });
                doc.sections.Add(perfSection);

                // ========== 第十一章：参考资源 ==========
                var resourceSection = new ESDocSection { sectionTitle = "🔗 参考资源与扩展阅读" };
                resourceSection.content.Add(new ESDocLink
                {
                    displayText = "Unity官方NavMesh文档",
                    url = "https://docs.unity3d.com/Manual/nav-NavigationSystem.html",
                    description = "Unity官方导航系统完整文档"
                });
                resourceSection.content.Add(new ESDocLink
                {
                    displayText = "NavMeshAgent API参考",
                    url = "https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent.html",
                    description = "NavMeshAgent类的API文档"
                });
                resourceSection.content.Add(new ESDocLink
                {
                    displayText = "Unity Learn - AI导航教程",
                    url = "https://learn.unity.com/tutorial/navigation-basics",
                    description = "Unity官方学习平台的交互式教程"
                });
                doc.sections.Add(resourceSection);

                // 保存文档
                string savePath = "Assets/Plugins/ES/Obsolete/Assets_ES_Legacy/Documentation";
                if (!AssetDatabase.IsValidFolder(savePath))
                {
                    string[] folders = savePath.Split('/');
                    string parentPath = "";
                    foreach (var folder in folders)
                    {
                        if (string.IsNullOrEmpty(folder)) continue;
                        string newPath = string.IsNullOrEmpty(parentPath) ? folder : $"{parentPath}/{folder}";
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            AssetDatabase.CreateFolder(parentPath, folder);
                        }
                        parentPath = newPath;
                    }
                }

                string assetPath = $"{savePath}/{doc.documentTitle}.asset";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                AssetDatabase.CreateAsset(doc, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = doc;
                EditorUtility.DisplayDialog("成功", $"Navmesh演示文档已创建！\n路径: {assetPath}\n\n可在文档阅读器中查看完整内容。", "确定");

                ESDocWindow.UsingWindow?.ForceMenuTreeRebuild();
            }
        }
        #endregion

        #region 创建文档
        [Serializable]
        public class Page_CreateDocument : ESWindowPageBase
        {
            [Title("创建新文档", "快速创建各类文档", bold: true, titleAlignment: TitleAlignments.Centered)]

            [BoxGroup("基本信息")]
            [LabelText("文档标题"), Space(5)]
            public string documentTitle = "新文档";

            [BoxGroup("基本信息")]
            [LabelText("文档分类"), Space(5)]
            [ValueDropdown("GetCategoryOptions")]
            public string documentCategory = "通用";

            [BoxGroup("基本信息")]
            [LabelText("作者"), Space(5)]
            public string author = "";

            [BoxGroup("创建选项")]
            [LabelText("使用模板")]
            [ValueDropdown("GetTemplateOptions")]
            public string selectedTemplate = "空白文档";

            [BoxGroup("创建选项")]
            [LabelText("保存路径"), FolderPath]
            public string savePath = "Assets/Plugins/ES/Obsolete/Assets_ES_Legacy/Documentation";

            private IEnumerable<string> GetCategoryOptions()
            {
                return new List<string> { "通用", "API文档", "教程", "设计文档", "技术规范", "用户手册", "更新日志", "最佳实践" };
            }

            private IEnumerable<string> GetTemplateOptions()
            {
                return new List<string> { "空白文档", "API文档模板", "教程模板", "设计文档模板", "技术规范模板" };
            }

            [Button("创建文档", ButtonHeight = 60), GUIColor(0.3f, 0.9f, 0.3f)]
            public void CreateDocument()
            {
                if (string.IsNullOrEmpty(documentTitle))
                {
                    EditorUtility.DisplayDialog("错误", "请输入文档标题！", "确定");
                    return;
                }

                // 创建保存路径
                if (!AssetDatabase.IsValidFolder(savePath))
                {
                    string[] folders = savePath.Split('/');
                    string parentPath = "";
                    foreach (var folder in folders)
                    {
                        if (string.IsNullOrEmpty(folder)) continue;
                        string newPath = string.IsNullOrEmpty(parentPath) ? folder : $"{parentPath}/{folder}";
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            AssetDatabase.CreateFolder(parentPath, folder);
                        }
                        parentPath = newPath;
                    }
                }

                // 创建文档资产
                var doc = ScriptableObject.CreateInstance<ESDocumentPageBase>();
                doc.documentTitle = documentTitle;
                doc.category = documentCategory;
                doc.author = author;
                doc.createDate = DateTime.Now.ToString("yyyy-MM-dd");
                doc.lastModified = DateTime.Now.ToString("yyyy-MM-dd");

                // 根据模板初始化内容
                InitializeFromTemplate(doc, selectedTemplate);

                // 保存资产
                string assetPath = $"{savePath}/{documentTitle}.asset";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                AssetDatabase.CreateAsset(doc, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("成功", $"文档已创建: {assetPath}", "确定");

                // 刷新窗口
                ESDocWindow.UsingWindow?.ForceMenuTreeRebuild();

                // 选中新创建的文档
                Selection.activeObject = doc;
            }

            private void InitializeFromTemplate(ESDocumentPageBase doc, string template)
            {
                switch (template)
                {
                    case "API文档模板":
                        InitializeAPITemplate(doc);
                        break;
                    case "教程模板":
                        InitializeTutorialTemplate(doc);
                        break;
                    case "设计文档模板":
                        InitializeDesignTemplate(doc);
                        break;
                    case "技术规范模板":
                        InitializeTechnicalTemplate(doc);
                        break;
                    default:
                        InitializeBlankTemplate(doc);
                        break;
                }
            }

            private void InitializeBlankTemplate(ESDocumentPageBase doc)
            {
                var section = new ESDocSection { sectionTitle = "简介" };
                section.content.Add(new ESDocText { content = "在此添加文档内容..." });
                doc.sections.Add(section);
            }

            private void InitializeAPITemplate(ESDocumentPageBase doc)
            {
                // 概述章节
                var overview = new ESDocSection { sectionTitle = "概述" };
                overview.content.Add(new ESDocText { content = "API的基本描述和用途..." });
                doc.sections.Add(overview);

                // 快速开始
                var quickStart = new ESDocSection { sectionTitle = "快速开始" };
                quickStart.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = "// 示例代码\npublic class Example\n{\n    void Start()\n    {\n        // 在此添加代码\n    }\n}"
                });
                doc.sections.Add(quickStart);

                // API参考
                var apiRef = new ESDocSection { sectionTitle = "API参考" };
                var table = new ESDocTable { tableTitle = "方法列表" };
                table.headers = new List<string> { "方法名", "参数", "返回值", "说明" };
                table.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "MethodName", "param1, param2", "void", "方法说明" }
                });
                apiRef.content.Add(table);
                doc.sections.Add(apiRef);

                // 注意事项
                var notes = new ESDocSection { sectionTitle = "注意事项" };
                notes.content.Add(new ESDocAlert
                {
                    alertType = ESDocAlert.AlertType.Warning,
                    title = "重要",
                    content = "使用此API时需要注意..."
                });
                doc.sections.Add(notes);
            }

            private void InitializeTutorialTemplate(ESDocumentPageBase doc)
            {
                // 教程目标
                var goals = new ESDocSection { sectionTitle = "学习目标" };
                var goalsList = new ESDocUnorderedList();
                goalsList.items = new List<string> { "目标1", "目标2", "目标3" };
                goals.content.Add(goalsList);
                doc.sections.Add(goals);

                // 准备工作
                var preparation = new ESDocSection { sectionTitle = "准备工作" };
                preparation.content.Add(new ESDocText { content = "开始之前你需要..." });
                doc.sections.Add(preparation);

                // 步骤
                var steps = new ESDocSection { sectionTitle = "操作步骤" };
                var stepsList = new ESDocOrderedList();
                stepsList.items = new List<string> { "步骤1: ...", "步骤2: ...", "步骤3: ..." };
                steps.content.Add(stepsList);
                doc.sections.Add(steps);

                // 完整示例
                var example = new ESDocSection { sectionTitle = "完整示例" };
                example.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = "// 完整示例代码\n"
                });
                doc.sections.Add(example);

                // 总结
                var summary = new ESDocSection { sectionTitle = "总结" };
                summary.content.Add(new ESDocText { content = "通过本教程你学会了..." });
                doc.sections.Add(summary);
            }

            private void InitializeDesignTemplate(ESDocumentPageBase doc)
            {
                // 设计目标
                var goals = new ESDocSection { sectionTitle = "设计目标" };
                goals.content.Add(new ESDocText { content = "本设计旨在..." });
                doc.sections.Add(goals);

                // 系统架构
                var architecture = new ESDocSection { sectionTitle = "系统架构" };
                architecture.content.Add(new ESDocImage { caption = "架构图" });
                architecture.content.Add(new ESDocText { content = "架构说明..." });
                doc.sections.Add(architecture);

                // 技术栈
                var techStack = new ESDocSection { sectionTitle = "技术栈" };
                var stackTable = new ESDocTable { tableTitle = "技术选型" };
                stackTable.headers = new List<string> { "组件", "技术", "版本", "备注" };
                stackTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "前端", "Unity", "2022.3", "" }
                });
                techStack.content.Add(stackTable);
                doc.sections.Add(techStack);

                // 接口设计
                var interfaces = new ESDocSection { sectionTitle = "接口设计" };
                interfaces.content.Add(new ESDocCodeBlock { language = "csharp", code = "// 接口定义\n" });
                doc.sections.Add(interfaces);

                // 风险评估
                var risks = new ESDocSection { sectionTitle = "风险评估" };
                risks.content.Add(new ESDocAlert
                {
                    alertType = ESDocAlert.AlertType.Warning,
                    title = "潜在风险",
                    content = "需要注意的风险点..."
                });
                doc.sections.Add(risks);
            }

            private void InitializeTechnicalTemplate(ESDocumentPageBase doc)
            {
                // 规范说明
                var intro = new ESDocSection { sectionTitle = "规范说明" };
                intro.content.Add(new ESDocText { content = "本技术规范定义了..." });
                doc.sections.Add(intro);

                // 命名规范
                var naming = new ESDocSection { sectionTitle = "命名规范" };
                var namingTable = new ESDocTable { tableTitle = "命名规则" };
                namingTable.headers = new List<string> { "类型", "规则", "示例", "说明" };
                namingTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "类名", "PascalCase", "PlayerController", "使用大驼峰命名法" }
                });
                namingTable.rows.Add(new ESDocTableRow
                {
                    cells = new List<string> { "方法名", "PascalCase", "GetPlayerData", "使用大驼峰命名法" }
                });
                naming.content.Add(namingTable);
                doc.sections.Add(naming);

                // 代码规范
                var codeStyle = new ESDocSection { sectionTitle = "代码规范" };
                codeStyle.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = "// 推荐写法\npublic class Example\n{\n    private int _value;\n    \n    public void DoSomething()\n    {\n        // 实现\n    }\n}"
                });
                doc.sections.Add(codeStyle);

                // 注释规范
                var comments = new ESDocSection { sectionTitle = "注释规范" };
                comments.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = "/// <summary>\n/// 方法说明\n/// </summary>\n/// <param name=\"value\">参数说明</param>\n/// <returns>返回值说明</returns>\npublic int Calculate(int value)\n{\n    return value * 2;\n}"
                });
                doc.sections.Add(comments);

                // 参考资料
                var references = new ESDocSection { sectionTitle = "参考资料" };
                references.content.Add(new ESDocLink
                {
                    displayText = "C# 编码规范",
                    url = "https://docs.microsoft.com/zh-cn/dotnet/csharp/fundamentals/coding-style/coding-conventions",
                    description = "微软官方C#编码规范"
                });
                doc.sections.Add(references);
            }
        }

        #region 文档模板
        [Serializable]
        public class Page_DocumentTemplates : ESWindowPageBase
        {
            [Title("文档模板", "预定义的文档模板", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 20), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string info = "选择一个模板快速开始";

            [Button("API文档模板", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void CreateAPITemplate()
            {
                CreateTemplateDocument("API文档模板", "API");
            }

            [Button("教程文档模板", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void CreateTutorialTemplate()
            {
                CreateTemplateDocument("教程文档模板", "教程");
            }

            [Button("设计文档模板", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
            public void CreateDesignTemplate()
            {
                CreateTemplateDocument("设计文档模板", "设计");
            }

            [Button("技术规范模板", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_06")]
            public void CreateTechnicalTemplate()
            {
                CreateTemplateDocument("技术规范模板", "技术规范");
            }

            private void CreateTemplateDocument(string title, string category)
            {
                if (ESDocWindow.MenuItems.TryGetValue(ESDocWindow.PageName_CreateDocument, out var item))
                {
                    ESDocWindow.UsingWindow.pageCreateDocument.documentTitle = title;
                    ESDocWindow.UsingWindow.pageCreateDocument.documentCategory = category;
                    ESDocWindow.UsingWindow.MenuTree.Selection.Add(item);
                }
            }
        }
        #endregion

        #region 内容元素参考
        [Serializable]
        public class Page_ContentElementsReference : ESWindowPageBase
        {
            [Title("内容元素参考", "所有可用的文档内容元素", bold: true, titleAlignment: TitleAlignments.Centered)]

            [InfoBox("ES文档系统支持以下内容元素类型，您可以在文档中组合使用这些元素来创建丰富的内容。", InfoMessageType.Info)]

            [TabGroup("元素类型", "文本类")]
            [Title("📝 文本元素")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string textInfo = "ESDocText - 普通文本段落\n支持多行文本，是最基本的内容元素。";

            [TabGroup("元素类型", "文本类")]
            [Title("💬 引用块")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string quoteInfo = "ESDocQuote - 引用块\n用于引用重要内容或第三方资料，支持来源标注。";

            [TabGroup("元素类型", "文本类")]
            [Title("⚠️ 警告框")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string alertInfo = "ESDocAlert - 警告提示框\n支持Info、Success、Warning、Error四种类型，用于突出显示重要信息。";

            [TabGroup("元素类型", "代码类")]
            [Title("💻 代码块")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string codeInfo = "ESDocCodeBlock - 代码块\n支持多种编程语言语法高亮：\n• C#\n• JavaScript\n• Python\n• Java\n• C++\n• XML/JSON\n• SQL\n• HTML/CSS";

            [TabGroup("元素类型", "列表类")]
            [Title("📋 无序列表")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string unorderedInfo = "ESDocUnorderedList - 无序列表\n使用圆点标记的列表项。";

            [TabGroup("元素类型", "列表类")]
            [Title("🔢 有序列表")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string orderedInfo = "ESDocOrderedList - 有序列表\n使用数字标记的列表项，自动编号。";

            [TabGroup("元素类型", "表格类")]
            [Title("📊 表格")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string tableInfo = "ESDocTable - 表格\n支持自定义列标题和行数据，适合展示结构化信息。\n特性：\n• 可拖拽调整行列\n• 支持表格标题\n• 自动生成Markdown/HTML表格";

            [TabGroup("元素类型", "媒体类")]
            [Title("🖼️ 图片")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string imageInfo = "ESDocImage - 图片\n支持Unity资产和外部图片路径，可添加图片说明。";

            [TabGroup("元素类型", "媒体类")]
            [Title("🔗 超链接")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string linkInfo = "ESDocLink - 超链接\n支持显示文本、URL地址和可选描述，自动在HTML中添加target='_blank'。";

            [TabGroup("元素类型", "格式类")]
            [Title("➖ 分隔线")]
            [DisplayAsString(fontSize: 14), HideLabel]
            public string dividerInfo = "ESDocDivider - 分隔线\n在内容之间添加水平分隔线。";

            [Title("使用示例")]
            [Button("查看完整示例文档", ButtonHeight = 50), GUIColor(0.3f, 0.8f, 0.9f)]
            public void OpenExampleDocument()
            {
                CreateExampleDocument();
            }

            private void CreateExampleDocument()
            {
                var doc = ScriptableObject.CreateInstance<ESDocumentPageBase>();
                doc.documentTitle = "内容元素示例";
                doc.category = "示例";
                doc.author = "系统";
                doc.createDate = DateTime.Now.ToString("yyyy-MM-dd");
                doc.lastModified = DateTime.Now.ToString("yyyy-MM-dd");

                // 文本示例
                var textSection = new ESDocSection { sectionTitle = "文本元素" };
                textSection.content.Add(new ESDocText
                {
                    content = "这是一个普通的文本段落。可以包含多行内容，支持基本的文本格式。"
                });
                textSection.content.Add(new ESDocQuote
                {
                    quoteText = "这是一个引用块的示例。",
                    source = "引用来源"
                });
                textSection.content.Add(new ESDocAlert
                {
                    alertType = ESDocAlert.AlertType.Info,
                    title = "提示",
                    content = "这是一个信息提示框。"
                });
                doc.sections.Add(textSection);

                // 列表示例
                var listSection = new ESDocSection { sectionTitle = "列表元素" };
                listSection.content.Add(new ESDocUnorderedList
                {
                    items = new List<string> { "无序列表项1", "无序列表项2", "无序列表项3" }
                });
                listSection.content.Add(new ESDocOrderedList
                {
                    items = new List<string> { "有序列表项1", "有序列表项2", "有序列表项3" }
                });
                doc.sections.Add(listSection);

                // 代码示例
                var codeSection = new ESDocSection { sectionTitle = "代码元素" };
                codeSection.content.Add(new ESDocCodeBlock
                {
                    language = "csharp",
                    code = "public class Example\n{\n    public void HelloWorld()\n    {\n        Debug.Log(\"Hello, World!\");\n    }\n}"
                });
                doc.sections.Add(codeSection);

                // 表格示例
                var tableSection = new ESDocSection { sectionTitle = "表格元素" };
                var table = new ESDocTable { tableTitle = "示例表格" };
                table.headers = new List<string> { "列1", "列2", "列3" };
                table.rows.Add(new ESDocTableRow { cells = new List<string> { "数据1", "数据2", "数据3" } });
                table.rows.Add(new ESDocTableRow { cells = new List<string> { "数据4", "数据5", "数据6" } });
                tableSection.content.Add(table);
                doc.sections.Add(tableSection);

                // 链接示例
                var linkSection = new ESDocSection { sectionTitle = "链接和媒体" };
                linkSection.content.Add(new ESDocLink
                {
                    displayText = "Unity官方文档",
                    url = "https://docs.unity3d.com",
                    description = "Unity引擎的官方文档网站"
                });
                linkSection.content.Add(new ESDocDivider());
                linkSection.content.Add(new ESDocImage { caption = "示例图片" });
                doc.sections.Add(linkSection);

                // 保存资产
                string path = "Assets/Plugins/ES/Obsolete/Assets_ES_Legacy/Documentation/内容元素示例.asset";
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string[] folders = dir.Split('/');
                    string parentPath = "";
                    foreach (var folder in folders)
                    {
                        if (string.IsNullOrEmpty(folder)) continue;
                        string newPath = string.IsNullOrEmpty(parentPath) ? folder : $"{parentPath}/{folder}";
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            AssetDatabase.CreateFolder(parentPath, folder);
                        }
                        parentPath = newPath;
                    }
                }

                path = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CreateAsset(doc, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = doc;
                EditorUtility.DisplayDialog("成功", "示例文档已创建！", "确定");

                ESDocWindow.UsingWindow?.ForceMenuTreeRebuild();
            }
        }
        #endregion
        #endregion
}

