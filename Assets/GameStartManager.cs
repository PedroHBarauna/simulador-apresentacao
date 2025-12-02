using UnityEngine;
using UnityEngine.UI;

public class GameStartManager : MonoBehaviour
{
    [Header("Fade")]
    public Image fadeImage;           // Imagem full-screen preta no Canvas
    public float fadeDuration = 5f;   // Duração do fade out

    [Header("Menu Objects")]
    public GameObject menuPanel;      // Painel do menu
    public GameObject blurImage;      // Imagem de blur
    public AudioSource menuAudio;     // AudioSource do menu

    [Header("Camera")]
    public Animator mainCameraAnimator;   // Animator da Main Camera
    public string gameStartAnim = "CameraStart"; // Nome da animação de início do jogo

    private bool isStarting = false;

    public void StartGame()
    {
        if (!isStarting)
            StartCoroutine(FadeOutAndStartGame());
    }

    private System.Collections.IEnumerator FadeOutAndStartGame()
    {
        Debug.Log("Entrou no StartGame");
        isStarting = true;

        if (menuPanel) menuPanel.SetActive(false);
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = new Color(0, 0, 0, 0);

        float timer = 0f;
        while (timer < fadeDuration)
        {

            timer += Time.deltaTime;
            float alpha = Mathf.Clamp01(timer / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        // Desabilitar menus e blur

        if (blurImage) blurImage.SetActive(false);
        if (menuAudio) menuAudio.Stop();

        if (mainCameraAnimator)
        {
            mainCameraAnimator.Play(gameStartAnim);
        }

        StartCoroutine(FadeIn());
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(timer / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        fadeImage.gameObject.SetActive(false);
    }
}
