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
        mainMenu.SetActive(false);
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

        // Aqui você pode iniciar o script da cutscene
        FindFirstObjectByType<GameStartManager>()?.StartGame();

        // OU carregar a cena do jogo:
        // SceneManager.LoadScene("NomeDaCena");
    }
}
