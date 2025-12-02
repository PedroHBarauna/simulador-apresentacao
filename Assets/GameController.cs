using UnityEngine;
using UnityEngine.UI; // <-- Pode ser removido se não usar mais o 'Button' ou outros elementos
using TMPro; // <-- NOVO: ESSENCIAL PARA TEXTMESHPRO

public class GameController : MonoBehaviour
{
    [Header("Camera Movement")]
    //public Transform cameraTargetPos;   // Posição final da câmera
    public float cameraMoveSpeed = 2f;

    [Header("UI")]
    public GameObject presentationPanel; // Painel com botão iniciar/parar
    public GameObject mainMenu;
    public Button actionButton;          // Botão Iniciar/Parar
    public TMP_Text actionButtonText;
    public TMP_Text timerText;
    public GameObject blurImage;
    public AudioSource menuAudio;

    [Header("Results")]
    public GameObject resultsPanel;      // Painel final com nota e resumo
    public TMP_Text scoreText;
    public TMP_Text summaryText;


    [Header("External Scripts")]
    // public AudioAnalyzer audioAnalyzer;   // Seu script já existente


    private bool isPresenting = false;
    private float counter = 0f;

    private void Update()
    {
        if (isPresenting)
        {
            counter += Time.deltaTime;
            timerText.text = counter.ToString("0.0") + "s";
        }
    }

    public void StartPresentationMode()
    {
        // Mostrar o painel com botão iniciar
        presentationPanel.SetActive(true);

        // Configurar botão como "Iniciar Apresentação"
        actionButtonText.text = "Iniciar Apresentação";
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(StartPresentation);

        // Mover a câmera
        //StartCoroutine(MoveCameraToPosition());
    }

    // private System.Collections.IEnumerator MoveCameraToPosition()
    // {
    //     Camera cam = Camera.main;

    //     while (Vector3.Distance(cam.transform.position, cameraTargetPos.position) > 0.1f)
    //     {
    //         cam.transform.position = Vector3.Lerp(
    //             cam.transform.position,
    //             cameraTargetPos.position,
    //             Time.deltaTime * cameraMoveSpeed
    //         );

    //         cam.transform.rotation = Quaternion.Lerp(
    //             cam.transform.rotation,
    //             cameraTargetPos.rotation,
    //             Time.deltaTime * cameraMoveSpeed
    //         );

    //         yield return null;
    //     }
    // }

    // Quando o usuário clica em "Iniciar Apresentação"
    private void StartPresentation()
    {
        isPresenting = true;
        counter = 0f;

        actionButtonText.text = "Parar Apresentação";
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(StopPresentation);
    }

    // Quando o usuário clica em "Parar Apresentação"
    private void StopPresentation()
    {
        isPresenting = false;
        presentationPanel.SetActive(false);
        counter = 0f;
        // // Rodar AudioAnalyzer
        // float score = audioAnalyzer.CalculateScore();
        // string summary = audioAnalyzer.GetSummary();

        // Mostrar painel final
        resultsPanel.SetActive(true);
        // scoreText.text = "Nota: " + score.ToString("0.0");
        // summaryText.text = summary;

        // Esconde painel da apresentação

    }

    private void ResetPresentation()
    {
        counter = 0f;
        timerText.text = "0.0s";

        resultsPanel.SetActive(false);
        StartPresentationMode();
    }

    public void OnRestartButtonClicked()
    {
        ResetPresentation();
    }

    public void BackToMenu()
    {
        // Aqui você pode adicionar lógica para voltar ao menu principal
        Debug.Log("Voltando ao menu principal...");
        Debug.Log(mainMenu.activeSelf);

        //Carregando GameObject do Menus
        mainMenu.SetActive(true);
        blurImage.SetActive(true);
        menuAudio.Play();

        Debug.Log(mainMenu.activeSelf);
        presentationPanel.SetActive(false);
        resultsPanel.SetActive(false);
    }
}
