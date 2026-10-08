using System.Collections.Generic;
using UnityEngine;

namespace RacingProject.Enemy
{
    // Осевые линии дорог для ИИ врагов: точки через несколько метров вдоль каждой дороги.
    // Заполняется в редакторе по сплайнам Road Architect (Tools/Unity/BakeRoadNetwork.cs): сборка RacingProject
    // не видит код Road Architect, поэтому линии хранятся в сцене готовыми.
    // EnemyCarController притягивает к оси точки своего пути, чтобы машины ехали по полосам, а не по кромке.
    public class RoadNetwork : MonoBehaviour
    {
        [System.Serializable]
        public class Road
        {
            public string name;
            [Tooltip("Половина ширины проезжей части без обочин, м")]
            public float halfWidth = 10f;
            public Vector3[] points = new Vector3[0];
        }

        [SerializeField] private Road[] roads = new Road[0];
        [Tooltip("Размер ячейки сетки поиска, м")]
        [SerializeField] private float cellSize = 25f;

        public static RoadNetwork Instance { get; private set; }

        // Ячейка сетки → отрезки дорог (номер дороги, номер точки начала отрезка)
        private readonly Dictionary<Vector2Int, List<Vector2Int>> grid = new Dictionary<Vector2Int, List<Vector2Int>>();

        public void SetRoads(Road[] value)
        {
            roads = value;
            BuildGrid();
        }

        private void Awake()
        {
            Instance = this;
            BuildGrid();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void BuildGrid()
        {
            grid.Clear();
            for (int r = 0; r < roads.Length; r++)
            {
                Vector3[] points = roads[r].points;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    // Отрезок короче ячейки: достаточно занести его в ячейки обоих концов
                    AddToCell(Cell(points[i]), r, i);
                    Vector2Int other = Cell(points[i + 1]);
                    if (other != Cell(points[i]))
                        AddToCell(other, r, i);
                }
            }
        }

        private void AddToCell(Vector2Int cell, int road, int index)
        {
            if (!grid.TryGetValue(cell, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>();
                grid.Add(cell, list);
            }
            list.Add(new Vector2Int(road, index));
        }

        private Vector2Int Cell(Vector3 p)
        {
            return new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));
        }

        // Ближайшая точка оси дороги в пределах maxDistance (по горизонтали), направление дороги и её полуширина
        public bool Nearest(Vector3 position, float maxDistance, out Vector3 center, out Vector3 direction, out float halfWidth)
        {
            center = position;
            direction = Vector3.forward;
            halfWidth = 0f;
            float best = maxDistance * maxDistance;
            bool found = false;
            Vector2Int c = Cell(position);
            int reach = Mathf.CeilToInt(maxDistance / cellSize);
            for (int dx = -reach; dx <= reach; dx++)
            for (int dz = -reach; dz <= reach; dz++)
            {
                if (!grid.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out List<Vector2Int> list)) continue;
                foreach (Vector2Int seg in list)
                {
                    Road road = roads[seg.x];
                    Vector3 a = road.points[seg.y], b = road.points[seg.y + 1];
                    Vector3 ab = b - a;
                    ab.y = 0f;
                    Vector3 ap = position - a;
                    ap.y = 0f;
                    float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                    Vector3 p = a + (b - a) * t;
                    Vector3 offset = position - p;
                    offset.y = 0f;
                    float d = offset.sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    found = true;
                    center = p;
                    direction = ab.normalized;
                    halfWidth = road.halfWidth;
                }
            }
            return found;
        }
    }
}
