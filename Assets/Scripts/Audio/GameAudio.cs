using UnityEngine;

// One place for every audio clip in the game. The PacStudent builder fills this from "Assets/Audio Clips" using the
// clip names below; clips can also be dragged onto Assets/Audio Clips/GameAudio.asset by hand.
[CreateAssetMenu(fileName = "GameAudio", menuName = "PacStudent/Game Audio")]
public class GameAudio : ScriptableObject
{
    [Header("Music")]
    public AudioClip startMusic;      // Music_StartScene
    public AudioClip introMusic;      // Music_Intro
    public AudioClip normalMusic;     // Music_GhostNormal
    public AudioClip scaredMusic;     // Music_GhostScared
    public AudioClip ghostDeadMusic;  // Music_GhostDead

    [Header("PacStudent")]
    public AudioClip move;            // SFX_Move (moving, no pellet ahead)
    public AudioClip moveEat;         // SFX_EatPellet (moving onto a pellet)
    public AudioClip wallBump;        // SFX_WallCollision
    public AudioClip death;           // SFX_Death

    [Header("Pickups")]
    public AudioClip ghostEat;        // SFX_EatGhost
    public AudioClip cherryEat;       // SFX_EatCherry

    [Header("UI")]
    public AudioClip buttonHover;     // SFX_EatPellet
    public AudioClip buttonClick;     // SFX_Gulp
}
