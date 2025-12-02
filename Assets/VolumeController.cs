using UnityEngine;

public class VolumeController : MonoBehaviour
{
    public AudioSource musicSource;

    public void SetVolume(float volume)
    {
        musicSource.volume = volume;
    }
}
