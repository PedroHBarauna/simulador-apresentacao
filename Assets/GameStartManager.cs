using UnityEngine;
using UnityEngine.UI;

public class GameStartManager : MonoBehaviour
{
    [Header("Fade")]
    public Image fadeImage;
    public Animator camAnimator;

    public float fadeDuration = 2f;

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
        camAnimator.enabled = false;
        // Remova: camAnimator.speed = 0f; (a menos que a animação não deva rodar)

        Camera cam = Camera.main;
        AudioListener audioListener = cam.GetComponent<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
        Vector3 startPosition = cam.transform.position;
        Quaternion startRotation = cam.transform.rotation;

        Vector3 targetPosition = new Vector3(8.4f, 22f, 137f);
        Quaternion targetRotation = Quaternion.Euler(25f, 180f, 0f);

        float duration = 1.5f; // Duração do movimento em segundos
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Opcional: Easing para movimento mais suave (ex: SmoothStep)
            // t = t * t * (3f - 2f * t);

            cam.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            cam.transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);

            elapsed += UnityEngine.Time.deltaTime;
            yield return null; // Espera até o próximo frame
        }

        // Garante que a câmera atinja a posição e rotação exatas no final
        cam.transform.position = targetPosition;
        cam.transform.rotation = targetRotation;
    }
}
