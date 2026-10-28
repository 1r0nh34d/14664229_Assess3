using UnityEngine;

[CreateAssetMenu(fileName = "GameAudio", menuName = "PacStudent/Game Audio")]
public class GameAudio : ScriptableObject
{
    [Header("Music")]
    public AudioClip startMusic;
    public AudioClip introMusic;
    public AudioClip normalMusic;
    public AudioClip scaredMusic;
    public AudioClip ghostDeadMusic;

    [Header("PacStudent")]
    public AudioClip move;
    public AudioClip moveEat;
    public AudioClip wallBump;
    public AudioClip death;

    [Header("Pickups")]
    public AudioClip ghostEat;
    public AudioClip cherryEat;

    [Header("UI")]
    public AudioClip buttonHover;
    public AudioClip buttonClick;
}
