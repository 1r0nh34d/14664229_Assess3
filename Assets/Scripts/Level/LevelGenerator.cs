using UnityEngine;

// Holds the level layout (the A3 levelMap) and the grid helpers used by PacStudent, the ghosts and the editor builder.
// Grid cells are Vector2Int(x = column, y = row) with row 0 at the TOP of the map.
public class LevelGenerator : MonoBehaviour
{
    // Top-left quadrant of the level.
    // 0 empty, 1 outside corner, 2 outside wall, 3 inside corner, 4 inside wall,
    // 5 pellet, 6 power pellet, 7 T junction, 8 ghost exit wall
    public static readonly int[,] levelMap =
    {
        {1,2,2,2,2,2,2,2,2,2,2,2,2,7},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,4},
        {2,6,4,0,0,4,5,4,0,0,0,4,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,3},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,5},
        {2,5,3,4,4,3,5,3,3,5,3,4,4,4},
        {2,5,3,4,4,3,5,4,4,5,3,4,4,3},
        {2,5,5,5,5,5,5,4,4,5,5,5,5,4},
        {1,2,2,2,2,1,5,4,3,4,4,3,0,4},
        {0,0,0,0,0,2,5,4,3,4,4,3,0,3},
        {0,0,0,0,0,2,5,4,4,0,0,0,0,0},
        {0,0,0,0,0,2,5,4,4,0,3,4,4,8},
        {2,2,2,2,2,1,5,3,3,0,4,0,0,0},
        {0,0,0,0,0,0,5,0,0,0,4,0,0,0},
    };

    public static readonly Vector2Int Up = new Vector2Int(0, -1);
    public static readonly Vector2Int Down = new Vector2Int(0, 1);
    public static readonly Vector2Int Left = new Vector2Int(-1, 0);
    public static readonly Vector2Int Right = new Vector2Int(1, 0);

    // Inside of the ghost spawn box (the empty cells between the ghost exit walls)
    public static readonly RectInt SpawnArea = new RectInt(11, 13, 6, 3);

    static int[,] fullMap;

    public static int[,] FullMap => fullMap ?? (fullMap = BuildFullMap());
    public static int Rows => FullMap.GetLength(0);
    public static int Cols => FullMap.GetLength(1);

    // Mirror the quadrant horizontally, then vertically (the bottom row of the quadrant is the middle row and is not repeated)
    static int[,] BuildFullMap()
    {
        int quadRows = levelMap.GetLength(0);
        int quadCols = levelMap.GetLength(1);
        int rows = quadRows * 2 - 1;
        int cols = quadCols * 2;
        int[,] map = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                map[r, c] = levelMap[r < quadRows ? r : rows - 1 - r, c < quadCols ? c : cols - 1 - c];
        return map;
    }

    public static bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.x < Cols && cell.y >= 0 && cell.y < Rows;

    public static int TileAt(Vector2Int cell) => InBounds(cell) ? FullMap[cell.y, cell.x] : -1;

    public static bool IsWallTile(int tile) => (tile >= 1 && tile <= 4) || tile == 7 || tile == 8;

    public static bool IsWall(Vector2Int cell) => IsWallTile(TileAt(cell));

    public static Vector3 CellToWorld(Vector2Int cell) =>
        new Vector3(cell.x - (Cols - 1) / 2f, (Rows - 1) / 2f - cell.y, 0f);

    public static Vector2Int WorldToCell(Vector3 position) =>
        new Vector2Int(Mathf.RoundToInt(position.x + (Cols - 1) / 2f), Mathf.RoundToInt((Rows - 1) / 2f - position.y));

    // ---------- Wall rotation (used by the editor builder to lay out the level) ----------

    // Ordered so that rotating a piece +90 degrees maps each direction to the next one
    static readonly Vector2Int[] RotationOrder = { Left, Down, Right, Up };

    static bool IsStraight(int tile) => tile == 2 || tile == 4 || tile == 8;

    static bool StraightIsHorizontal(Vector2Int cell)
    {
        int horizontal = (IsWall(cell + Left) ? 1 : 0) + (IsWall(cell + Right) ? 1 : 0);
        int vertical = (IsWall(cell + Up) ? 1 : 0) + (IsWall(cell + Down) ? 1 : 0);
        return horizontal >= vertical;
    }

    // How strongly the wall next to cell (in direction dir) joins onto it
    static int ConnectScore(Vector2Int cell, Vector2Int dir)
    {
        Vector2Int neighbour = cell + dir;
        if (!IsWall(neighbour)) return 0;
        if (IsStraight(TileAt(neighbour))) return StraightIsHorizontal(neighbour) == (dir.y == 0) ? 2 : 0;
        return 1;
    }

    // Z rotation (degrees) for the wall sprite at this cell.
    // Base sprites: corners join Left+Down, the T junction stem points Down,
    // outside/ghost walls are horizontal and inside walls are vertical.
    public static float GetWallRotation(Vector2Int cell)
    {
        switch (TileAt(cell))
        {
            case 2:
            case 8:
                return StraightIsHorizontal(cell) ? 0f : 90f;
            case 4:
                return StraightIsHorizontal(cell) ? 90f : 0f;
            case 1:
            case 3:
            {
                int best = 0, bestScore = -1;
                for (int k = 0; k < 4; k++)
                {
                    int a = ConnectScore(cell, RotationOrder[k]);
                    int b = ConnectScore(cell, RotationOrder[(k + 1) % 4]);
                    if (a == 0 || b == 0) continue;
                    if (a + b > bestScore) { bestScore = a + b; best = k; }
                }
                return best * 90f;
            }
            case 7:
                for (int k = 0; k < 4; k++)
                {
                    Vector2Int dir = RotationOrder[k];
                    if (ConnectScore(cell, dir) > 0 && ConnectScore(cell, -dir) == 0) return (k - 1) * 90f;
                }
                return 0f;
        }
        return 0f;
    }
}
