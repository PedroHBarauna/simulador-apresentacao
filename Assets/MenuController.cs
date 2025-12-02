using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject settingsMenu;

    public void QuitGame()
    {
        Debug.Log("Saindo do jogo...");
        Application.Quit();
    }

    public void OpenSettings()
    {
        Debug.Log("Inciando Configurações");
        Debug.Log(mainMenu.activeSelf);
        mainMenu.SetActive(false);
        Debug.Log(mainMenu.activeSelf);
        settingsMenu.SetActive(true);
    }

    public void BackToMenu()
    {
        settingsMenu.SetActive(false);
        mainMenu.SetActive(true);
    }

    public void StartGame()
    {
        Debug.Log("Iniciando jogo...");
        Debug.Log(mainMenu.activeSelf);

        // Aqui você pode iniciar o script da cutscene
        FindFirstObjectByType<GameStartManager>()?.StartGame();

        // OU carregar a cena do jogo:
        // SceneManager.LoadScene("NomeDaCena");
    }
}
