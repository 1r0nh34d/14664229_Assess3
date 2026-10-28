using System.Collections.Generic;
using UnityEngine;

// A pellet or power pellet sitting on a grid cell. Keeps a registry so PacStudent can tell if the next cell has food.
public class Pellet : MonoBehaviour
{
    public bool isPowerPellet;

    static readonly Dictionary<Vector2Int, Pellet> pellets = new Dictionary<Vector2Int, Pellet>();

    public static int Remaining => pellets.Count;
    public Vector2Int Cell { get; private set; }

    void Awake()
    {
        Cell = LevelGenerator.WorldToCell(transform.position);
        pellets[Cell] = this;
    }

    void OnDestroy() => Unregister();

    public void Unregister()
    {
        if (pellets.TryGetValue(Cell, out Pellet pellet) && pellet == this) pellets.Remove(Cell);
    }

    public static bool ExistsAt(Vector2Int cell) => pellets.ContainsKey(cell);
}
