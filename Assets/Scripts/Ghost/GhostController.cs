using System.Collections.Generic;
using UnityEngine;

public enum GhostState { Normal, Scared, Recovering, Dead }

public class GhostController : MonoBehaviour
{
    [Range(1, 4)] public int ghostNumber = 1;
    public Vector2Int startCell;
    public Animator animator;
    public float pacStudentSpeed = 5f;

    public GhostState State { get; private set; } = GhostState.Normal;
    public bool IsVulnerable => State == GhostState.Scared || State == GhostState.Recovering;

    Vector2Int cell;
    Vector2Int previousCell;
    Vector2Int targetCell;
    Vector2Int moveDirection = LevelGenerator.Right;
    Vector3 lerpFrom;
    Vector3 lerpTo;
    float lerpProgress;
    bool isLerping;
    bool frozen = true;
    int waypointIndex = -1;
    int stepsTowardWaypoint;

    float NormalSpeed => pacStudentSpeed * 0.9f;
    float Speed => State == GhostState.Normal ? NormalSpeed : NormalSpeed * 0.5f;

    void Start()
    {
        ResetToStart();
    }

    void Update()
    {
        if (frozen) return;

        if (State == GhostState.Dead)
        {
            SwimHome();
            return;
        }

        if (isLerping)
        {
            lerpProgress += Time.deltaTime * Speed;
            transform.position = Vector3.Lerp(lerpFrom, lerpTo, Mathf.Min(lerpProgress, 1f));
            if (lerpProgress >= 1f)
            {
                isLerping = false;
                cell = targetCell;
            }
        }

        if (!isLerping) ChooseNextMove();
    }

    void SwimHome()
    {
        Vector3 home = LevelGenerator.CellToWorld(startCell);
        transform.position = Vector3.MoveTowards(transform.position, home, Speed * Time.deltaTime);
        if (transform.position != home) return;

        cell = startCell;
        previousCell = startCell;
        isLerping = false;
        GameManager.Instance.GhostReachedHome(this);
    }

    void ChooseNextMove()
    {
        List<Vector2Int> options = new List<Vector2Int>();
        foreach (Vector2Int dir in LevelGenerator.Directions)
        {
            Vector2Int next = cell + dir;
            if (next != previousCell && LevelGenerator.IsGhostWalkable(cell, next)) options.Add(dir);
        }
        if (options.Count == 0)
        {
            foreach (Vector2Int dir in LevelGenerator.Directions)
                if (LevelGenerator.IsGhostWalkable(cell, cell + dir)) options.Add(dir);
        }
        if (options.Count == 0) return;

        Vector2Int choice;
        if (LevelGenerator.IsInSpawn(cell)) choice = Closest(options, ExitTarget());
        else if (State != GhostState.Normal || ghostNumber == 1) choice = AwayFromPacStudent(options);
        else if (ghostNumber == 2) choice = TowardPacStudent(options);
        else if (ghostNumber == 3) choice = options[Random.Range(0, options.Count)];
        else choice = Clockwise(options);

        StepTo(choice);
    }

    void StepTo(Vector2Int direction)
    {
        previousCell = cell;
        targetCell = cell + direction;
        lerpFrom = transform.position;
        lerpTo = LevelGenerator.CellToWorld(targetCell);
        lerpProgress = 0f;
        isLerping = true;
        moveDirection = direction;
        PlayStateAnimation();
    }

    Vector2Int ExitTarget()
    {
        bool top = ghostNumber == 1 || ghostNumber == 3;
        int row = top ? LevelGenerator.SpawnArea.yMin - 2 : LevelGenerator.SpawnArea.yMax + 1;
        int leftGapCol = LevelGenerator.SpawnArea.xMin + 2;
        int col = Mathf.Abs(cell.x - leftGapCol) <= Mathf.Abs(cell.x - (leftGapCol + 1)) ? leftGapCol : leftGapCol + 1;
        return new Vector2Int(col, row);
    }

    float DistanceToPacStudent(Vector2Int fromCell) =>
        Vector2.Distance(LevelGenerator.CellToWorld(fromCell), GameManager.Instance.pacStudent.transform.position);

    Vector2Int AwayFromPacStudent(List<Vector2Int> options)
    {
        float current = DistanceToPacStudent(cell);
        List<Vector2Int> valid = options.FindAll(d => DistanceToPacStudent(cell + d) >= current);
        if (valid.Count == 0) valid = options;
        return valid[Random.Range(0, valid.Count)];
    }

    Vector2Int TowardPacStudent(List<Vector2Int> options)
    {
        float current = DistanceToPacStudent(cell);
        List<Vector2Int> valid = options.FindAll(d => DistanceToPacStudent(cell + d) <= current);
        if (valid.Count == 0) valid = options;
        return valid[Random.Range(0, valid.Count)];
    }

    static readonly Vector2Int[] OuterLoop =
    {
        new Vector2Int(1, 1), new Vector2Int(12, 1), new Vector2Int(12, 5), new Vector2Int(15, 5),
        new Vector2Int(15, 1), new Vector2Int(26, 1), new Vector2Int(26, 8), new Vector2Int(21, 8),
        new Vector2Int(21, 20), new Vector2Int(26, 20), new Vector2Int(26, 27), new Vector2Int(15, 27),
        new Vector2Int(15, 23), new Vector2Int(12, 23), new Vector2Int(12, 27), new Vector2Int(1, 27),
        new Vector2Int(1, 20), new Vector2Int(6, 20), new Vector2Int(6, 8), new Vector2Int(1, 8),
    };

    Vector2Int Clockwise(List<Vector2Int> options)
    {
        if (waypointIndex < 0)
        {
            waypointIndex = 0;
            for (int i = 1; i < OuterLoop.Length; i++)
                if (Vector2Int.Distance(cell, OuterLoop[i]) < Vector2Int.Distance(cell, OuterLoop[waypointIndex])) waypointIndex = i;
            stepsTowardWaypoint = 0;
        }

        stepsTowardWaypoint++;
        if (cell == OuterLoop[waypointIndex] || stepsTowardWaypoint > 40)
        {
            waypointIndex = (waypointIndex + 1) % OuterLoop.Length;
            stepsTowardWaypoint = 0;
        }

        return Closest(options, OuterLoop[waypointIndex]);
    }

    Vector2Int Closest(List<Vector2Int> options, Vector2Int target)
    {
        Vector2Int best = options[0];
        float bestDistance = float.MaxValue;
        foreach (Vector2Int dir in options)
        {
            float distance = Vector2Int.Distance(cell + dir, target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = dir;
            }
        }
        return best;
    }

    public void SetState(GhostState newState)
    {
        if (newState == GhostState.Normal && State != GhostState.Normal) waypointIndex = -1;
        State = newState;
        if (newState == GhostState.Dead) isLerping = false;
        PlayStateAnimation();
    }

    static string DirectionName(Vector2Int direction)
    {
        if (direction == LevelGenerator.Up) return "Up";
        if (direction == LevelGenerator.Down) return "Down";
        if (direction == LevelGenerator.Left) return "Left";
        return "Right";
    }

    void PlayStateAnimation()
    {
        string state;
        switch (State)
        {
            case GhostState.Scared: state = "Scared_" + DirectionName(moveDirection); break;
            case GhostState.Recovering: state = "Recovering"; break;
            case GhostState.Dead: state = "Dead"; break;
            default: state = "Walk_" + DirectionName(moveDirection); break;
        }
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state)) animator.Play(state, 0, 0f);
    }

    public void SetFrozen(bool value)
    {
        frozen = value;
        animator.speed = value ? 0f : 1f;
    }

    public void ResetToStart()
    {
        cell = startCell;
        previousCell = startCell;
        targetCell = startCell;
        transform.position = LevelGenerator.CellToWorld(startCell);
        isLerping = false;
        waypointIndex = -1;
        stepsTowardWaypoint = 0;
        moveDirection = ghostNumber % 2 == 1 ? LevelGenerator.Up : LevelGenerator.Down;
        SetState(GhostState.Normal);
    }
}
