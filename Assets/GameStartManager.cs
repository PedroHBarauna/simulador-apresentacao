using UnityEngine;
using UnityEngine.UI;

public class GameStartManager : MonoBehaviour
{
    [Header("Fade")]
    public Image fadeImage;
    public float fadeDuration = 3f;

    [Header("Menu Objects")]
    public GameObject menuPanel;
    public GameObject blurImage;
    public AudioSource menuAudio;

    [Header("Game Logic")]
    public GameController gameController;

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
        Debug.Log(menuPanel.activeSelf);

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

        //Para animação da camera
        StartCoroutine(MoveCameraToPosition());

        // CHAMA O SCRIPT DO JOGO
        if (gameController)
            gameController.StartPresentationMode();

        // Fade in
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

    private System.Collections.IEnumerator MoveCameraToPosition()
    {
        Camera cam = Camera.main;
        Vector3 targetPosition = new Vector3(0, 1, -10);
        yield return null;
    }
}
