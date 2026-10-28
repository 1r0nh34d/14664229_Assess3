using UnityEngine;
using UnityEngine.InputSystem;

// Grid based movement: PacStudent lerps from one cell centre to the next at a fixed, frame-rate independent speed.
// lastInput is the last key pressed, currentInput is the direction PacStudent is actually travelling.
public class PacStudentController : MonoBehaviour
{
    public float speed = 5f; // tiles per second
    public Vector2Int startCell = new Vector2Int(1, 1);

    public Animator animator;
    public AudioSource moveSource;
    public GameAudio audioLibrary;
    public ParticleSystem dust;

    Key lastInput = Key.None;
    Key currentInput = Key.None;

    Vector2Int cell;
    Vector2Int targetCell;
    Vector3 lerpFrom;
    Vector3 lerpTo;
    float lerpProgress;
    bool isLerping;
    bool isMoving;

    public Vector2Int Cell => cell;

    void Start()
    {
        ResetToStart();
    }

    void Update()
    {
        GatherInput();

        if (isLerping)
        {
            // 1 tile per lerp, so progress per second equals tiles per second
            lerpProgress += Time.deltaTime * speed;
            transform.position = Vector3.Lerp(lerpFrom, lerpTo, Mathf.Min(lerpProgress, 1f));
            if (lerpProgress >= 1f)
            {
                isLerping = false;
                cell = targetCell;
            }
        }

        if (!isLerping) TryMove();
    }

    // Only overwritten when the player presses a new key
    void GatherInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.wKey.wasPressedThisFrame) lastInput = Key.W;
        else if (keyboard.aKey.wasPressedThisFrame) lastInput = Key.A;
        else if (keyboard.sKey.wasPressedThisFrame) lastInput = Key.S;
        else if (keyboard.dKey.wasPressedThisFrame) lastInput = Key.D;
    }

    static Vector2Int KeyToDirection(Key key)
    {
        switch (key)
        {
            case Key.W: return LevelGenerator.Up;
            case Key.A: return LevelGenerator.Left;
            case Key.S: return LevelGenerator.Down;
            case Key.D: return LevelGenerator.Right;
            default: return Vector2Int.zero;
        }
    }

    static bool IsWalkable(Vector2Int cell)
    {
        int tile = LevelGenerator.TileAt(cell);
        return tile == 0 || tile == 5 || tile == 6;
    }

    bool CanMove(Key key) => key != Key.None && IsWalkable(cell + KeyToDirection(key));

    // Try lastInput first; if that is blocked keep going in currentInput; if that is blocked too, stop
    void TryMove()
    {
        if (CanMove(lastInput))
        {
            currentInput = lastInput;
            StartLerp(KeyToDirection(currentInput));
        }
        else if (CanMove(currentInput))
        {
            StartLerp(KeyToDirection(currentInput));
        }
        else
        {
            StopMoving();
        }
    }

    void StartLerp(Vector2Int direction)
    {
        Vector2Int next = cell + direction;
        targetCell = next;
        lerpFrom = transform.position;
        lerpTo = LevelGenerator.CellToWorld(next);
        lerpProgress = 0f;
        isLerping = true;
        isMoving = true;

        PlayDirectionAnimation(direction);
        animator.speed = 1f;
        SetDust(true);

        // Separate sound for swimming onto a pellet and swimming into empty space
        AudioClip clip = Pellet.ExistsAt(next) ? audioLibrary.moveEat : audioLibrary.move;
        if (moveSource != null && clip != null && (moveSource.clip != clip || !moveSource.isPlaying))
        {
            moveSource.clip = clip;
            moveSource.loop = true;
            moveSource.Play();
        }
    }

    void StopMoving()
    {
        if (!isMoving) return;
        isMoving = false;
        animator.speed = 0f;
        SetDust(false);
        if (moveSource != null) moveSource.Stop();
    }

    void PlayDirectionAnimation(Vector2Int direction)
    {
        string state = "Walk_Right";
        if (direction == LevelGenerator.Left) state = "Walk_Left";
        else if (direction == LevelGenerator.Up) state = "Walk_Up";
        else if (direction == LevelGenerator.Down) state = "Walk_Down";
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state)) animator.Play(state, 0, 0f);
    }

    void SetDust(bool on)
    {
        if (dust == null) return;
        ParticleSystem.EmissionModule emission = dust.emission;
        emission.enabled = on;
        if (on && !dust.isPlaying) dust.Play();
    }

    public void ResetToStart()
    {
        cell = startCell;
        targetCell = startCell;
        transform.position = LevelGenerator.CellToWorld(startCell);
        isLerping = false;
        isMoving = false;
        lastInput = Key.None;
        currentInput = Key.None;
        SetDust(false);
        if (moveSource != null) moveSource.Stop();
        if (animator != null)
        {
            // Facing right, waiting for the first key press
            animator.Play("Walk_Right", 0, 0f);
            animator.speed = 0f;
        }
    }
}
