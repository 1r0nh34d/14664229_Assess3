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
    public ParticleSystem wallBump;
    public ParticleSystem deathEffect;

    Key lastInput = Key.None;
    Key currentInput = Key.None;

    Vector2Int cell;
    Vector2Int targetCell;
    Vector3 lerpFrom;
    Vector3 lerpTo;
    float lerpProgress;
    bool isLerping;
    bool isMoving;
    bool controlEnabled;
    bool pressedThisFrame;

    public Vector2Int Cell => cell;

    void Start()
    {
        ResetToStart();
    }

    void Update()
    {
        if (!controlEnabled) return;

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
        pressedThisFrame = false;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.wKey.wasPressedThisFrame) SetLastInput(Key.W);
        else if (keyboard.aKey.wasPressedThisFrame) SetLastInput(Key.A);
        else if (keyboard.sKey.wasPressedThisFrame) SetLastInput(Key.S);
        else if (keyboard.dKey.wasPressedThisFrame) SetLastInput(Key.D);
    }

    void SetLastInput(Key key)
    {
        lastInput = key;
        pressedThisFrame = true;
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

    // Stepping off the left/right edge of the map from the tunnel row is a teleport
    static bool IsTeleportExit(Vector2Int cell) =>
        cell.y >= 0 && cell.y < LevelGenerator.Rows && (cell.x < 0 || cell.x >= LevelGenerator.Cols);

    bool CanMove(Key key)
    {
        if (key == Key.None) return false;
        Vector2Int next = cell + KeyToDirection(key);
        return IsWalkable(next) || IsTeleportExit(next);
    }

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
        if (IsTeleportExit(next))
        {
            // Jump to the tunnel on the other side, then keep moving inwards
            cell.x = next.x < 0 ? LevelGenerator.Cols - 1 : 0;
            transform.position = LevelGenerator.CellToWorld(cell);
            next = cell + direction;
        }

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
        if (isMoving)
        {
            isMoving = false;
            animator.speed = 0f;
            SetDust(false);
            if (moveSource != null) moveSource.Stop();

            // First frame of running into a wall
            Vector2Int blocked = KeyToDirection(currentInput);
            if (LevelGenerator.IsWall(cell + blocked)) BumpWall(blocked);
        }
        else if (pressedThisFrame && LevelGenerator.IsWall(cell + KeyToDirection(lastInput)))
        {
            BumpWall(KeyToDirection(lastInput));
        }
    }

    // Particles and sound at the point where PacStudent meets the wall
    void BumpWall(Vector2Int direction)
    {
        Vector3 worldDirection = new Vector3(direction.x, -direction.y, 0f);
        if (wallBump != null)
        {
            wallBump.transform.position = transform.position + worldDirection * 0.5f;
            wallBump.Play();
        }
        GameManager.Instance.PlaySfx(audioLibrary.wallBump);
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

    // Collisions are registered with triggers (kinematic Rigidbody2D, no physics response).
    // Stay rather than Enter so anything already overlapping when control is enabled (the pellet under the
    // start cell, a shark that recovers on top of PacStudent) still counts.
    void OnTriggerStay2D(Collider2D other)
    {
        if (!controlEnabled) return;

        if (other.TryGetComponent(out Pellet pellet)) GameManager.Instance.EatPellet(pellet);
        else if (other.TryGetComponent(out Cherry cherry)) GameManager.Instance.EatCherry(cherry);
        else if (other.TryGetComponent(out GhostController ghost)) GameManager.Instance.PacStudentHitGhost(ghost);
    }

    public void SetControl(bool enabled)
    {
        controlEnabled = enabled;
        if (!enabled)
        {
            SetDust(false);
            if (moveSource != null) moveSource.Stop();
            if (animator != null) animator.speed = 0f;
        }
        else if (isMoving && animator != null)
        {
            animator.speed = 1f;
        }
    }

    public void PlayDeath()
    {
        SetDust(false);
        if (moveSource != null) moveSource.Stop();
        animator.speed = 1f;
        animator.Play("Dead", 0, 0f);
        if (deathEffect != null)
        {
            deathEffect.transform.position = transform.position;
            deathEffect.Play();
        }
        GameManager.Instance.PlaySfx(audioLibrary.death);
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
