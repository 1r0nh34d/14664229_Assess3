using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GhostMode { Normal, Scared, Recovering }

// Runs a round: countdown, score, lives, timers, the sharks' scared state, music, death and game over.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level")]
    public string levelKey = "Level1"; // PlayerPrefs key prefix
    public string levelName = "Level 1";
    public string startSceneName = "StartScene";

    [Header("References")]
    public PacStudentController pacStudent;
    public GhostController[] ghosts;
    public HUDController hud;
    public CherryController cherryController;
    public GameAudio audioLibrary;
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Rules")]
    public int startingLives = 3;
    public float scaredDuration = 10f;
    public float recoveringAt = 3f;
    public float deathSequenceLength = 2f;
    public float gameOverDelay = 3f;

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public float GameTime { get; private set; }
    public bool IsPlaying { get; private set; }
    public GhostMode Mode { get; private set; } = GhostMode.Normal;
    public GameAudio Audio => audioLibrary;

    float scaredTimer;
    bool timerRunning;
    bool gameOver;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Lives = startingLives;
        hud.SetLevelName(levelName);
        hud.SetScore(Score);
        hud.SetLives(Lives);
        hud.SetTime(0f);
        hud.SetScaredTimer(0f);
        StartCoroutine(RoundStart());
    }

    // 3, 2, 1, GO! over a blocking image with the intro music, then the round starts
    IEnumerator RoundStart()
    {
        SetActorsFrozen(true);
        PlayMusic(audioLibrary.introMusic, false);

        hud.ShowBlocker(true);
        foreach (string text in new[] { "3", "2", "1", "GO!" })
        {
            hud.ShowCountdown(text);
            yield return new WaitForSeconds(1f);
        }
        hud.ShowCountdown(null);
        hud.ShowBlocker(false);

        musicSource.Stop();
        IsPlaying = true;
        timerRunning = true;
        SetActorsFrozen(false);
        UpdateMusic();
    }

    void Update()
    {
        if (timerRunning)
        {
            GameTime += Time.deltaTime;
            hud.SetTime(GameTime);
        }

        if (!IsPlaying || Mode == GhostMode.Normal) return;

        scaredTimer -= Time.deltaTime;
        if (scaredTimer <= 0f) SetMode(GhostMode.Normal);
        else if (Mode == GhostMode.Scared && scaredTimer <= recoveringAt) SetMode(GhostMode.Recovering);
        hud.SetScaredTimer(Mathf.Max(scaredTimer, 0f));
    }

    void SetActorsFrozen(bool frozen)
    {
        pacStudent.SetControl(!frozen);
        foreach (GhostController ghost in ghosts) ghost.SetFrozen(frozen);
    }

    // ---------- Scoring ----------

    public void AddScore(int amount)
    {
        Score += amount;
        hud.SetScore(Score);
    }

    public void EatPellet(Pellet pellet)
    {
        pellet.Unregister();
        Destroy(pellet.gameObject);

        if (pellet.isPowerPellet)
        {
            AddScore(50);
            scaredTimer = scaredDuration;
            SetMode(GhostMode.Scared);
        }
        else
        {
            AddScore(10);
        }

        if (Pellet.Remaining == 0) StartCoroutine(GameOver());
    }

    public void EatCherry(Cherry cherry)
    {
        AddScore(100);
        PlaySfx(audioLibrary.cherryEat);
        cherryController.CherryEaten(cherry);
    }

    // ---------- Ghost states ----------

    // Dead sharks keep their Dead state; everyone else follows the global mode
    void SetMode(GhostMode mode)
    {
        Mode = mode;
        if (mode == GhostMode.Normal)
        {
            scaredTimer = 0f;
            hud.SetScaredTimer(0f);
        }
        foreach (GhostController ghost in ghosts)
            if (ghost.State != GhostState.Dead) ghost.SetState((GhostState)mode);
        UpdateMusic();
    }

    public void PacStudentHitGhost(GhostController ghost)
    {
        if (!IsPlaying || ghost.State == GhostState.Dead) return;

        if (ghost.IsVulnerable)
        {
            ghost.SetState(GhostState.Dead);
            AddScore(300);
            PlaySfx(audioLibrary.ghostEat);
            UpdateMusic();
        }
        else
        {
            StartCoroutine(PacStudentDeath());
        }
    }

    // A dead shark back at its spawn position rejoins in the current global mode; the music changes back
    // once no other shark is dead
    public void GhostReachedHome(GhostController ghost)
    {
        ghost.SetState((GhostState)Mode);
        UpdateMusic();
    }

    bool AnyGhostDead()
    {
        foreach (GhostController ghost in ghosts)
            if (ghost.State == GhostState.Dead) return true;
        return false;
    }

    // ---------- Death and game over ----------

    IEnumerator PacStudentDeath()
    {
        IsPlaying = false;
        SetActorsFrozen(true);
        musicSource.Stop();
        Lives--;
        hud.SetLives(Lives);
        pacStudent.PlayDeath();

        yield return new WaitForSeconds(deathSequenceLength);

        if (Lives <= 0)
        {
            StartCoroutine(GameOver());
            yield break;
        }

        // Back to the top-left corner waiting for input, sharks back home and Normal
        pacStudent.ResetToStart();
        foreach (GhostController ghost in ghosts) ghost.ResetToStart();
        Mode = GhostMode.Normal;
        scaredTimer = 0f;
        hud.SetScaredTimer(0f);

        IsPlaying = true;
        SetActorsFrozen(false);
        UpdateMusic();
    }

    IEnumerator GameOver()
    {
        if (gameOver) yield break;
        gameOver = true;
        IsPlaying = false;
        timerRunning = false;
        SetActorsFrozen(true);
        cherryController.StopSpawning();
        musicSource.Stop();

        hud.ShowBlocker(true);
        hud.ShowGameOver();
        hud.SetButtonsInteractable(false);
        SaveHighScore();

        yield return new WaitForSeconds(gameOverDelay);
        SceneManager.LoadScene(startSceneName);
    }

    // Saved when the score beats the previous best, or equals it in a lower time
    void SaveHighScore()
    {
        string scoreKey = levelKey + "_HighScore";
        string timeKey = levelKey + "_BestTime";
        bool hasPrevious = PlayerPrefs.HasKey(scoreKey);
        int previousScore = PlayerPrefs.GetInt(scoreKey, 0);
        float previousTime = PlayerPrefs.GetFloat(timeKey, float.MaxValue);

        if (!hasPrevious || Score > previousScore || (Score == previousScore && GameTime < previousTime))
        {
            PlayerPrefs.SetInt(scoreKey, Score);
            PlayerPrefs.SetFloat(timeKey, GameTime);
            PlayerPrefs.Save();
        }
    }

    // ---------- Audio ----------

    void UpdateMusic()
    {
        if (!IsPlaying) return;
        AudioClip clip;
        if (AnyGhostDead()) clip = audioLibrary.ghostDeadMusic;
        else if (Mode == GhostMode.Normal) clip = audioLibrary.normalMusic;
        else clip = audioLibrary.scaredMusic;

        if (musicSource.clip != clip || !musicSource.isPlaying) PlayMusic(clip, true);
    }

    void PlayMusic(AudioClip clip, bool loop)
    {
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        if (clip != null) musicSource.Play();
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null) sfxSource.PlayOneShot(clip);
    }
}
