using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityEngine;

public class AudioAnalysisClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiUrl = "https://api.openai.com/v1/audio/transcriptions"; // Exemplo

    public AudioAnalysisClient(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<float> GetVoiceAnalysisScoreAsync(string audioFilePath)
    {
        // Lê o áudio
        byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath);

        // Monta o conteúdo da requisição
        var content = new MultipartFormDataContent();

        // Coloca o arquivo de áudio
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav"); // ou outro formato suportado
        content.Add(audioContent, "audio", Path.GetFileName(audioFilePath));

        // Adiciona o prompt / instruções
        var messages = new object[]
        {
            new { role = "system", content = "Você é um avaliador de apresentações acadêmicas." },
            new { role = "user", content = "Analise as características da voz e responda apenas com uma nota objetiva de 0 a 10." }
        };

        // Outras configurações, se o modelo multimodal suportar "modalities" etc
        var payload = new
        {
            model = "gpt-4o-audio-preview",  // exemplo, se o modelo suportar
            modalities = new string[] { "audio" },  // ou ["audio","text"] dependendo do endpoint
            messages
        };

        // Usa Newtonsoft.Json para serializar o payload
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var payloadContent = new StringContent(jsonPayload);
        payloadContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // Adiciona o JSON na requisição multipart
        content.Add(payloadContent, "payload");

        // Faz a requisição HTTP
        HttpResponseMessage response = await _httpClient.PostAsync(_apiUrl, content);
        string responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Debug.LogError($"❌ Erro na requisição: {(int)response.StatusCode} {response.ReasonPhrase}\nResposta: {responseString}");
            throw new Exception($"Erro da API: {responseString}");
        }

        Debug.Log($"✅ Resposta bruta da API:\n{responseString}");


        // ✅ Parseia a resposta usando Newtonsoft.Json (sem JsonDocument)
        dynamic root = JsonConvert.DeserializeObject(responseString);
        string scoreString = root.choices[0].message.content;

        // Converte a nota para float
        if (float.TryParse(scoreString, out float score))
        {
            return score;
        }
        else
        {
            throw new Exception("Formato inesperado da resposta: " + scoreString);
        }
    }
}
