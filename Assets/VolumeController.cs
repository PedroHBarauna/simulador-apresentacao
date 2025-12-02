using UnityEngine;

public class VolumeController : MonoBehaviour
{
    public AudioSource musicSource;

    public void SetVolume()
    {
        musicSource.volume = this.GetComponent<UnityEngine.UI.Slider>().value;
    }
}
