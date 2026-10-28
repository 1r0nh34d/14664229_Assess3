using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartSceneManager : MonoBehaviour
{
    public GameAudio audioLibrary;
    public AudioSource musicSource;
    public AudioSource sfxSource;

    public Text level1ScoreText;
    public Text level1TimeText;
    public Text level2ScoreText;
    public Text level2TimeText;

    public string level1Scene = "Level1";

    bool loading;

    void Start()
    {
        ShowRecord("Level1", level1ScoreText, level1TimeText);
        ShowRecord("Level2", level2ScoreText, level2TimeText);

        if (audioLibrary.startMusic != null)
        {
            musicSource.clip = audioLibrary.startMusic;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    static void ShowRecord(string levelKey, Text scoreText, Text timeText)
    {
        int score = PlayerPrefs.GetInt(levelKey + "_HighScore", 0);
        float time = PlayerPrefs.GetFloat(levelKey + "_BestTime", 0f);
        scoreText.text = "High Score: " + score.ToString("D6");
        timeText.text = "Time: " + TimeFormat.Format(time);
    }

    public void LoadLevel1() => StartCoroutine(LoadAfterClick(level1Scene));

    public void PressLevel2() => PlayClick();

    IEnumerator LoadAfterClick(string sceneName)
    {
        if (loading) yield break;
        loading = true;
        PlayClick();
        yield return new WaitForSeconds(0.25f);
        SceneManager.LoadScene(sceneName);
    }

    void PlayClick()
    {
        if (audioLibrary.buttonClick != null) sfxSource.PlayOneShot(audioLibrary.buttonClick);
    }

    public void PlayHover()
    {
        if (audioLibrary.buttonHover != null) sfxSource.PlayOneShot(audioLibrary.buttonHover);
    }
}
