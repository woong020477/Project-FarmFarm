// 역할: 통행 가능 셀과 방호 경로 탐색 데이터를 보관한다.
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FarmFarm/Defense Map")]
public sealed class DefenseMap : ScriptableObject
{
    public RectInt bounds;
    public Rect brickBounds, barrierBounds, patrolBounds;
    public Vector2Int[] blockedCells;
    [System.NonSerialized]
    private int[] distance;
    [System.NonSerialized]
    private bool[] blocked;
    private readonly List<Vector2> spawnPoints = new();
    private static readonly Vector2Int[] Steps =
    {
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.left,
        Vector2Int.down
    };
    private void OnEnable()
    {
        distance = null;
        blocked = null;
        spawnPoints.Clear();
    }

    public IReadOnlyList<Vector2> SpawnPoints
    {
        get
        {
            Initialize();
            return spawnPoints;
        }
    }

    private int Index(Vector2Int c) => c.x - bounds.xMin + (c.y - bounds.yMin) * bounds.width;
    public void Initialize()
    {
        if (distance != null)
            return;
        blocked = new bool[bounds.width * bounds.height];
        distance = new int[blocked.Length];
        for (int i = 0; i < distance.Length; i++)
            distance[i] = -1;
        foreach (var cell in blockedCells)
            if (bounds.Contains(cell))
                blocked[Index(cell)] = true;
        var queue = new Queue<Vector2Int>();
        foreach (var cell in bounds.allPositionsWithin)
        {
            Vector2 p = (Vector2)cell + Vector2.one * .5f;
            if (IsBlocked(cell) || barrierBounds.Contains(p))
                continue;
            float dx = Mathf.Max(barrierBounds.xMin - p.x, 0, p.x - barrierBounds.xMax);
            float dy = Mathf.Max(barrierBounds.yMin - p.y, 0, p.y - barrierBounds.yMax);
            if (dx + dy <= .51f)
            {
                distance[Index(cell)] = 0;
                queue.Enqueue(cell);
            }
        }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var step in Steps)
            {
                var n = cell + step;
                if (IsBlocked(n) || distance[Index(n)] >= 0)
                    continue;
                distance[Index(n)] = distance[Index(cell)] + 1;
                queue.Enqueue(n);
            }
        }

        foreach (var c in bounds.allPositionsWithin)
            if ((c.x == bounds.xMin || c.x == bounds.xMax - 1 || c.y == bounds.yMin || c.y == bounds.yMax - 1) && distance[Index(c)] > 0)
                spawnPoints.Add((Vector2)c + Vector2.one * .5f);
    }

    public bool IsBlocked(Vector2Int c) => !bounds.Contains(c) || blocked[Index(c)];
    public bool IsWalkable(Vector2 position)
    {
        Initialize();
        var c = Vector2Int.FloorToInt(position);
        return !IsBlocked(c) && distance[Index(c)] >= 0;
    }

    public bool Next(Vector2 position, out Vector2 target, out bool arrived)
    {
        Initialize();
        var cell = Vector2Int.FloorToInt(position);
        target = (Vector2)cell + Vector2.one * .5f;
        arrived = false;
        if (IsBlocked(cell) || distance[Index(cell)] < 0)
            return false;
        // Finish the current cell center before turning: never cut a wall corner.
        if (Vector2.Distance(position, target) > .001f)
            return true;
        int current = distance[Index(cell)];
        if (current == 0)
        {
            arrived = true;
            return true;
        }

        foreach (var step in Steps)
        {
            var n = cell + step;
            if (!IsBlocked(n) && distance[Index(n)] == current - 1)
            {
                target = (Vector2)n + Vector2.one * .5f;
                return true;
            }
        }

        return false;
    }

    public bool HasSight(Vector2 from, Vector2 to)
    {
        Initialize();
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 5);
        for (int i = 1; i < steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
            // Sandbags allow shooting across; other building/wall cells do not.
            if (!barrierBounds.Contains(p) && IsBlocked(Vector2Int.FloorToInt(p)))
                return false;
            if (brickBounds.Contains(p))
                return false;
        }

        return true;
    }
}
