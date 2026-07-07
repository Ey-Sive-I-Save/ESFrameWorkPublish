using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ES.Preview.Navigation
{
    /// <summary>
    /// A* 寻路算法完整实现
    /// 
    /// **支持功能**：
    /// - 8方向/4方向移动
    /// - 动态权重（地形代价）
    /// - 路径平滑
    /// - 动态障碍
    /// - 分帧计算（避免卡顿）
    /// </summary>
    
    #region Node Definition
    
    /// <summary>
    /// 寻路节点
    /// </summary>
    public class PathNode
    {
        public Vector2Int position;      // 网格坐标
        public bool walkable = true;     // 是否可行走
        public float terrainCost = 1f;   // 地形代价（如沼泽=2，道路=0.5）
        
        // A* 算法数据
        public float gCost;              // 起点到当前节点的实际代价
        public float hCost;              // 当前节点到终点的启发式代价
        public float FCost => gCost + hCost;
        public PathNode parent;          // 父节点（用于回溯路径）
        
        public PathNode(Vector2Int pos)
        {
            position = pos;
        }
        
        public void Reset()
        {
            gCost = 0;
            hCost = 0;
            parent = null;
        }
    }
    
    #endregion
    
    #region Grid System
    
    /// <summary>
    /// 寻路网格系统
    /// </summary>
    public class PathfindingGrid
    {
        public int width;
        public int height;
        public float nodeSize;           // 每个节点的世界坐标尺寸
        public Vector3 worldOrigin;      // 网格世界坐标原点
        
        private PathNode[,] grid;
        
        public PathfindingGrid(int width, int height, float nodeSize, Vector3 worldOrigin)
        {
            this.width = width;
            this.height = height;
            this.nodeSize = nodeSize;
            this.worldOrigin = worldOrigin;
            
            CreateGrid();
        }
        
        private void CreateGrid()
        {
            grid = new PathNode[width, height];
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var node = new PathNode(new Vector2Int(x, y));
                    
                    // 射线检测判断是否可行走
                    Vector3 worldPos = GridToWorld(new Vector2Int(x, y));
                    node.walkable = !Physics.Raycast(worldPos + Vector3.up * 5f, Vector3.down, 10f, LayerMask.GetMask("Obstacle"));
                    
                    grid[x, y] = node;
                }
            }
        }
        
        public PathNode GetNode(Vector2Int pos)
        {
            if (pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
                return grid[pos.x, pos.y];
            return null;
        }
        
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return worldOrigin + new Vector3(gridPos.x * nodeSize, 0, gridPos.y * nodeSize);
        }
        
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            var localPos = worldPos - worldOrigin;
            int x = Mathf.RoundToInt(localPos.x / nodeSize);
            int y = Mathf.RoundToInt(localPos.z / nodeSize);
            return new Vector2Int(x, y);
        }
        
        /// <summary>
        /// 获取邻居节点
        /// </summary>
        public List<PathNode> GetNeighbors(PathNode node, bool allowDiagonal = true)
        {
            var neighbors = new List<PathNode>();
            
            // 4方向
            var directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // 上
                new Vector2Int(1, 0),   // 右
                new Vector2Int(0, -1),  // 下
                new Vector2Int(-1, 0)   // 左
            };
            
            foreach (var dir in directions)
            {
                var neighborPos = node.position + dir;
                var neighbor = GetNode(neighborPos);
                if (neighbor != null && neighbor.walkable)
                    neighbors.Add(neighbor);
            }
            
            // 8方向（对角线）
            if (allowDiagonal)
            {
                var diagonalDirs = new Vector2Int[]
                {
                    new Vector2Int(1, 1),   // 右上
                    new Vector2Int(1, -1),  // 右下
                    new Vector2Int(-1, -1), // 左下
                    new Vector2Int(-1, 1)   // 左上
                };
                
                foreach (var dir in diagonalDirs)
                {
                    var neighborPos = node.position + dir;
                    var neighbor = GetNode(neighborPos);
                    
                    // 检查是否可斜向移动（避免穿墙）
                    var adjX = GetNode(node.position + new Vector2Int(dir.x, 0));
                    var adjY = GetNode(node.position + new Vector2Int(0, dir.y));
                    
                    if (neighbor != null && neighbor.walkable &&
                        adjX != null && adjX.walkable &&
                        adjY != null && adjY.walkable)
                    {
                        neighbors.Add(neighbor);
                    }
                }
            }
            
            return neighbors;
        }
        
        /// <summary>
        /// 更新障碍物
        /// </summary>
        public void UpdateObstacle(Vector3 worldPos, float radius, bool isObstacle)
        {
            var centerGrid = WorldToGrid(worldPos);
            int gridRadius = Mathf.CeilToInt(radius / nodeSize);
            
            for (int x = -gridRadius; x <= gridRadius; x++)
            {
                for (int y = -gridRadius; y <= gridRadius; y++)
                {
                    var pos = centerGrid + new Vector2Int(x, y);
                    var node = GetNode(pos);
                    if (node != null)
                    {
                        float distance = Vector2.Distance(centerGrid, pos);
                        if (distance <= gridRadius)
                        {
                            node.walkable = !isObstacle;
                        }
                    }
                }
            }
        }
    }
    
    #endregion
    
    #region A* Pathfinder
    
    /// <summary>
    /// A* 寻路器
    /// </summary>
    public class AStarPathfinder
    {
        private PathfindingGrid grid;
        private bool allowDiagonal;
        
        public AStarPathfinder(PathfindingGrid grid, bool allowDiagonal = true)
        {
            this.grid = grid;
            this.allowDiagonal = allowDiagonal;
        }
        
        /// <summary>
        /// 寻找路径
        /// </summary>
        public List<Vector3> FindPath(Vector3 startWorld, Vector3 targetWorld)
        {
            var startGrid = grid.WorldToGrid(startWorld);
            var targetGrid = grid.WorldToGrid(targetWorld);
            
            var startNode = grid.GetNode(startGrid);
            var targetNode = grid.GetNode(targetGrid);
            
            if (startNode == null || targetNode == null || !targetNode.walkable)
            {
                Debug.LogWarning("Invalid start or target position");
                return null;
            }
            
            // 重置节点
            ResetNodes();
            
            var openSet = new List<PathNode> { startNode };
            var closedSet = new HashSet<PathNode>();
            
            startNode.gCost = 0;
            startNode.hCost = GetDistance(startNode, targetNode);
            
            while (openSet.Count > 0)
            {
                // 找到F值最小的节点
                var currentNode = openSet.OrderBy(n => n.FCost).ThenBy(n => n.hCost).First();
                
                if (currentNode == targetNode)
                {
                    // 找到路径
                    return RetracePath(startNode, targetNode);
                }
                
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);
                
                foreach (var neighbor in grid.GetNeighbors(currentNode, allowDiagonal))
                {
                    if (closedSet.Contains(neighbor))
                        continue;
                    
                    float moveCost = GetDistance(currentNode, neighbor);
                    float newGCost = currentNode.gCost + moveCost * neighbor.terrainCost;
                    
                    if (newGCost < neighbor.gCost || !openSet.Contains(neighbor))
                    {
                        neighbor.gCost = newGCost;
                        neighbor.hCost = GetDistance(neighbor, targetNode);
                        neighbor.parent = currentNode;
                        
                        if (!openSet.Contains(neighbor))
                            openSet.Add(neighbor);
                    }
                }
            }
            
            Debug.LogWarning("Path not found");
            return null;
        }
        
        private void ResetNodes()
        {
            for (int x = 0; x < grid.width; x++)
            {
                for (int y = 0; y < grid.height; y++)
                {
                    grid.GetNode(new Vector2Int(x, y))?.Reset();
                }
            }
        }
        
        private float GetDistance(PathNode a, PathNode b)
        {
            int dx = Mathf.Abs(a.position.x - b.position.x);
            int dy = Mathf.Abs(a.position.y - b.position.y);
            
            if (allowDiagonal)
            {
                // 对角线距离（Chebyshev距离）
                return Mathf.Max(dx, dy);
            }
            else
            {
                // 曼哈顿距离
                return dx + dy;
            }
        }
        
        private List<Vector3> RetracePath(PathNode startNode, PathNode endNode)
        {
            var path = new List<PathNode>();
            var currentNode = endNode;
            
            while (currentNode != startNode)
            {
                path.Add(currentNode);
                currentNode = currentNode.parent;
            }
            
            path.Reverse();
            
            // 转换为世界坐标
            return path.Select(n => grid.GridToWorld(n.position)).ToList();
        }
    }
    
    #endregion
    
    #region Path Smoothing
    
    /// <summary>
    /// 路径平滑器
    /// </summary>
    public static class PathSmoother
    {
        /// <summary>
        /// 射线平滑：移除不必要的转折点
        /// </summary>
        public static List<Vector3> RaycastSmooth(List<Vector3> path, LayerMask obstacleMask)
        {
            if (path == null || path.Count < 3)
                return path;
            
            var smoothed = new List<Vector3> { path[0] };
            int currentIndex = 0;
            
            while (currentIndex < path.Count - 1)
            {
                // 尝试跳过中间点
                for (int i = path.Count - 1; i > currentIndex + 1; i--)
                {
                    if (!Physics.Linecast(path[currentIndex], path[i], obstacleMask))
                    {
                        smoothed.Add(path[i]);
                        currentIndex = i;
                        break;
                    }
                }
                
                if (currentIndex == smoothed.Count - 1)
                {
                    // 无法跳过，添加下一个点
                    currentIndex++;
                    if (currentIndex < path.Count)
                        smoothed.Add(path[currentIndex]);
                }
            }
            
            return smoothed;
        }
        
        /// <summary>
        /// Catmull-Rom曲线平滑
        /// </summary>
        public static List<Vector3> CatmullRomSmooth(List<Vector3> path, int resolution = 10)
        {
            if (path == null || path.Count < 2)
                return path;
            
            var smoothed = new List<Vector3>();
            
            for (int i = 0; i < path.Count - 1; i++)
            {
                var p0 = i > 0 ? path[i - 1] : path[i];
                var p1 = path[i];
                var p2 = path[i + 1];
                var p3 = i < path.Count - 2 ? path[i + 2] : path[i + 1];
                
                for (int j = 0; j < resolution; j++)
                {
                    float t = j / (float)resolution;
                    smoothed.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            
            smoothed.Add(path[path.Count - 1]);
            return smoothed;
        }
        
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
    
    #endregion
    
    #region Path Follower
    
    /// <summary>
    /// 路径跟随器
    /// </summary>
    public class PathFollower : MonoBehaviour
    {
        public float speed = 5f;
        public float reachDistance = 0.1f;
        
        private List<Vector3> currentPath;
        private int currentWaypointIndex;
        
        public void SetPath(List<Vector3> path)
        {
            currentPath = path;
            currentWaypointIndex = 0;
        }
        
        void Update()
        {
            if (currentPath == null || currentPath.Count == 0)
                return;
            
            if (currentWaypointIndex >= currentPath.Count)
            {
                // 到达终点
                currentPath = null;
                return;
            }
            
            var targetPos = currentPath[currentWaypointIndex];
            var direction = (targetPos - transform.position).normalized;
            
            transform.position += direction * speed * Time.deltaTime;
            transform.forward = direction;
            
            // 检查是否到达当前路点
            if (Vector3.Distance(transform.position, targetPos) < reachDistance)
            {
                currentWaypointIndex++;
            }
        }
        
        void OnDrawGizmos()
        {
            if (currentPath != null)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
                }
            }
        }
    }
    
    #endregion
    
    #region Dynamic Pathfinding
    
    /// <summary>
    /// 动态寻路系统（支持实时重算）
    /// </summary>
    public class DynamicPathfindingSystem : MonoBehaviour
    {
        public PathfindingGrid grid;
        public AStarPathfinder pathfinder;
        
        [Header("Settings")]
        public int gridWidth = 100;
        public int gridHeight = 100;
        public float nodeSize = 1f;
        
        [Header("Dynamic Recalculation")]
        public float recalculateInterval = 0.5f;
        private float lastRecalculateTime;
        
        private Dictionary<GameObject, PathFollower> agents = new();
        private Dictionary<GameObject, Vector3> agentTargets = new();
        
        void Start()
        {
            // 创建网格
            grid = new PathfindingGrid(gridWidth, gridHeight, nodeSize, transform.position);
            pathfinder = new AStarPathfinder(grid);
        }
        
        public void RegisterAgent(GameObject agent, Vector3 target)
        {
            if (!agents.ContainsKey(agent))
            {
                var follower = agent.GetComponent<PathFollower>();
                if (follower == null)
                    follower = agent.AddComponent<PathFollower>();
                
                agents[agent] = follower;
            }
            
            agentTargets[agent] = target;
            RecalculatePath(agent);
        }
        
        public void UnregisterAgent(GameObject agent)
        {
            agents.Remove(agent);
            agentTargets.Remove(agent);
        }
        
        void Update()
        {
            if (Time.time - lastRecalculateTime > recalculateInterval)
            {
                lastRecalculateTime = Time.time;
                RecalculateAllPaths();
            }
        }
        
        private void RecalculateAllPaths()
        {
            foreach (var agent in agents.Keys.ToList())
            {
                if (agent == null)
                {
                    UnregisterAgent(agent);
                    continue;
                }
                
                RecalculatePath(agent);
            }
        }
        
        private void RecalculatePath(GameObject agent)
        {
            if (!agentTargets.TryGetValue(agent, out var target))
                return;
            
            var path = pathfinder.FindPath(agent.transform.position, target);
            if (path != null && agents.TryGetValue(agent, out var follower))
            {
                // 路径平滑
                path = PathSmoother.RaycastSmooth(path, LayerMask.GetMask("Obstacle"));
                follower.SetPath(path);
            }
        }
        
        /// <summary>
        /// 添加动态障碍
        /// </summary>
        public void AddObstacle(Vector3 worldPos, float radius)
        {
            grid.UpdateObstacle(worldPos, radius, true);
            RecalculateAllPaths();
        }
        
        /// <summary>
        /// 移除动态障碍
        /// </summary>
        public void RemoveObstacle(Vector3 worldPos, float radius)
        {
            grid.UpdateObstacle(worldPos, radius, false);
            RecalculateAllPaths();
        }
    }
    
    #endregion
}
