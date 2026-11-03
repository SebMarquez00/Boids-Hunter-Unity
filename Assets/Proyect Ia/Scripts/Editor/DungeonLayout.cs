using System.Collections.Generic;
using UnityEngine;

// Distribucion de la variante C. Las coordenadas describen suelo, no rutas de IA.
public static class DungeonLayout
{
    public const int Width = 62;
    public const int Depth = 40;
    public static readonly RectInt Start = new RectInt(1, 0, 10, 8);
    public static readonly RectInt Exit = new RectInt(0, 30, 8, 8);
    public static readonly RectInt HuntingGround = new RectInt(46, 12, 16, 26);
    public static readonly RectInt[] SafeZones =
    {
        new RectInt(12, 25, 8, 4),
        new RectInt(40, 22, 4, 8),
        new RectInt(14, 0, 12, 8)
    };
    private static readonly RectInt[] RoomsAndPassages =
    {
        new RectInt(0, 12, 16, 10), new RectInt(22, 12, 16, 10), new RectInt(22, 29, 16, 10),
        new RectInt(8, 33, 38, 3), new RectInt(16, 12, 30, 3),
        new RectInt(28, 22, 3, 7), new RectInt(34, 22, 3, 7),
        new RectInt(5, 8, 3, 4), new RectInt(15, 29, 3, 4),
        new RectInt(37, 25, 3, 3), new RectInt(18, 8, 3, 4)
    };
    private static readonly RectInt[] Columns =
    {
        new RectInt(5, 15, 6, 4), new RectInt(27, 15, 6, 4)
    };

    public static Vector3 ToWorld(float x, float z, float height = 0f)
    {
        return new Vector3(x - Width * 0.5f, height, z - Depth * 0.5f);
    }

    public static bool IsWalkable(int x, int z)
    {
        Vector2Int cell = new Vector2Int(x, z);
        foreach (RectInt column in Columns)
            if (column.Contains(cell)) return false;
        if (Start.Contains(cell) || Exit.Contains(cell) || HuntingGround.Contains(cell)) return true;
        foreach (RectInt safeZone in SafeZones)
            if (safeZone.Contains(cell)) return true;
        foreach (RectInt floor in RoomsAndPassages)
            if (floor.Contains(cell)) return true;
        return false;
    }

    public static List<RectInt> GetSolidRectangles()
    {
        List<RectInt> rectangles = new List<RectInt>();
        // Une franjas iguales de filas consecutivas para evitar un objeto por celda.
        Dictionary<Vector2Int, int> previousRow = new Dictionary<Vector2Int, int>();
        for (int z = -1; z <= Depth; z++)
        {
            Dictionary<Vector2Int, int> currentRow = new Dictionary<Vector2Int, int>();
            int x = -1;
            while (x <= Width)
            {
                if (IsWalkable(x, z)) { x++; continue; }
                int start = x;
                while (x <= Width && !IsWalkable(x, z)) x++;
                Vector2Int span = new Vector2Int(start, x - start);
                int index;
                if (previousRow.TryGetValue(span, out index))
                {
                    RectInt rectangle = rectangles[index];
                    rectangle.height++;
                    rectangles[index] = rectangle;
                }
                else
                {
                    index = rectangles.Count;
                    rectangles.Add(new RectInt(start, z, x - start, 1));
                }
                currentRow.Add(span, index);
            }
            previousRow = currentRow;
        }
        return rectangles;
    }
}
