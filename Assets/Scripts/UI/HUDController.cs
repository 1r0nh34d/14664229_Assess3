using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    public Text scoreText;
    public Text timeText;
    public Text scaredTimerText;
    public Text levelNameText;
    public Text countdownText;
    public Text gameOverText;
    public Image blocker;
    public Image[] lifeIcons;
    public Button exitButton;
    public string startSceneName = "StartScene";

    void Awake()
    {
        ShowBlocker(false);
        ShowCountdown(null);
        gameOverText.gameObject.SetActive(false);
        SetScaredTimer(0f);
    }

    public void SetScore(int score) => scoreText.text = score.ToString("D6");

    public void SetTime(float seconds) => timeText.text = TimeFormat.Format(seconds);

    public void SetLevelName(string levelName) => levelNameText.text = levelName;

    public void SetLives(int lives)
    {
        for (int i = 0; i < lifeIcons.Length; i++) lifeIcons[i].enabled = i < lives;
    }

    public void SetScaredTimer(float seconds)
    {
        int whole = Mathf.CeilToInt(seconds);
        scaredTimerText.gameObject.SetActive(whole > 0);
        scaredTimerText.text = "Scared: " + whole;
    }

    public void ShowBlocker(bool show) => blocker.gameObject.SetActive(show);

    public void ShowCountdown(string text)
    {
        countdownText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        countdownText.text = text;
    }

    public void ShowGameOver() => gameOverText.gameObject.SetActive(true);

    public void SetButtonsInteractable(bool interactable) => exitButton.interactable = interactable;

    public void ExitToStart() => SceneManager.LoadScene(startSceneName);
}
