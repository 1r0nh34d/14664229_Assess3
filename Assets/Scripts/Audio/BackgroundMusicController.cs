using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicController : MonoBehaviour
{
    public AudioClip introMusic;
    public AudioClip normalMusic;
    public float maxIntroSeconds = 3f;

    AudioSource source;

    IEnumerator Start()
    {
        source = GetComponent<AudioSource>();

        if (introMusic != null)
        {
            source.clip = introMusic;
            source.loop = false;
            source.Play();
            yield return new WaitForSeconds(Mathf.Min(introMusic.length, maxIntroSeconds));
        }

        source.clip = normalMusic;
        source.loop = true;
        if (normalMusic != null) source.Play();
    }
}
