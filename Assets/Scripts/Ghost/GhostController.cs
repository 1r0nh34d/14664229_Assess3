using UnityEngine;

public enum GhostState { Normal, Scared, Recovering, Dead }

// A shark's state and animation. A dead shark comes back after deadDuration seconds in whatever state the other
// sharks are in (Normal, Scared or Recovering).
public class GhostController : MonoBehaviour
{
    [Range(1, 4)] public int ghostNumber = 1;
    public Vector2Int startCell;
    public Animator animator;
    public float deadDuration = 3f;

    public GhostState State { get; private set; } = GhostState.Normal;
    public bool IsVulnerable => State == GhostState.Scared || State == GhostState.Recovering;

    string facing = "Right";
    float deadTimer;
    bool frozen = true;

    void Start()
    {
        ResetToStart();
    }

    void Update()
    {
        if (frozen || State != GhostState.Dead) return;

        deadTimer -= Time.deltaTime;
        if (deadTimer <= 0f) GameManager.Instance.GhostRevived(this);
    }

    public void SetState(GhostState newState)
    {
        State = newState;
        if (newState == GhostState.Dead) deadTimer = deadDuration;
        PlayStateAnimation();
    }

    void PlayStateAnimation()
    {
        string state;
        switch (State)
        {
            case GhostState.Scared: state = "Scared_" + facing; break;
            case GhostState.Recovering: state = "Recovering"; break;
            case GhostState.Dead: state = "Dead"; break;
            default: state = "Walk_" + facing; break;
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
        transform.position = LevelGenerator.CellToWorld(startCell);
        deadTimer = 0f;
        SetState(GhostState.Normal);
    }
}
