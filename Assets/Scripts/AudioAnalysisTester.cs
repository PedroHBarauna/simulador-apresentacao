using System.Threading.Tasks;
using UnityEngine;

public class AudioAnalysisTester : MonoBehaviour
{
    [Header("Configurações")]
    [Tooltip("Caminho do arquivo de áudio a ser analisado (relativo à pasta do projeto).")]
    public string audioFilePath = "Assets/voz_teste.wav";

    [Tooltip("Sua chave da API OpenAI")]
    public string openAIApiKey = "INSIRA_SUA_CHAVE_AQUI";

    private async void Start()
    {
        Debug.Log("🎧 Iniciando análise de voz...");

        try
        {
            // Instancia o cliente com sua API Key
            var client = new AudioAnalysisClient(openAIApiKey);

            // Chama a função de análise (método assíncrono)
            float score = await client.GetVoiceAnalysisScoreAsync(audioFilePath);

            // Mostra o resultado no console
            Debug.Log($"✅ Nota final da apresentação: {score:F1}/10");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ Erro na análise: {ex.Message}");
        }
    }
}
