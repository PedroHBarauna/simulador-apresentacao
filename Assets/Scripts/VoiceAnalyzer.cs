using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VoiceAnalyzer : MonoBehaviour
{
    // Score de coerência/nexo
    private float coherenceScore = 0f;
    private string openAiApiKey = null;
    private string transcriptText = null; // Defina isso após transcrição
    public enum VoiceType { Masculina, Feminina }

    [System.Serializable]
    public class SegmentAnalysis
    {
        public float timeStamp;      // Tempo em segundos
        public float rms;            // Energia do segmento
        public float db;             // Volume em decibéis
        public float pitch;          // Frequência fundamental
        public bool isSilence;       // Se é silêncio
    }

    [HideInInspector]
    private AudioClip clip; // agora interno, apenas vindo do gravador
    public VoiceType tipoVoz = VoiceType.Masculina; // Configurável no Inspector
    public float segmentDuration = 1.0f; // Duração de cada segmento em segundos (padrão 1s)
    [Header("Detecção de Silêncio")]
    [Tooltip("RMS abaixo deste valor é considerado silêncio (0.001–0.005 recomendado)")]
    public float rmsSilenceThreshold = 0.0025f;
    [Tooltip("dBFS abaixo deste valor é considerado silêncio (ex.: -55 dBFS)")]
    public float dbFsSilenceThreshold = -55f;
    [Header("Debug")]
    [Tooltip("Loga informações de alguns segmentos para calibrar silêncio")]
    public bool debugSilence = false;
    [Tooltip("Quantos primeiros segmentos logar integralmente")]
    public int debugLogFirstSegments = 6;
    [Tooltip("Após os primeiros, logar a cada N segmentos")]
    public int debugLogEvery = 10;

    private float[] samples;
    private float sampleRate;
    private float rmsValue;
    private float dbValue;
    private float pitchValue;
    private float silencePercentage;

    // Arrays com análise segmentada
    public SegmentAnalysis[] segmentAnalyses;
    private float avgRms;
    private float avgDb;
    private float avgPitch;
    private int activeSpeechSegments;

    // Métricas de ritmo e pausas
    private int speechBursts;              // Número de "rajadas" de fala
    private float avgSpeechBurstDuration;  // Duração média de cada rajada
    private float avgPauseDuration;        // Duração média de pausas
    private int excessivePausesCount;      // Pausas muito longas (>10s)
    private float longestPauseDuration;    // Pausa mais longa detectada

    // Métricas de expressividade
    private float energyVariationRange;    // Variação de energia (ênfase)
    private float speechContinuityPercent; // % tempo falando

    void Awake()
    {
        openAiApiKey = LoadOpenAIApiKey();
    }

    // Método público dedicado a gravações vindas do RealtimeRecorder
    public void AnalyzeRecordedClip(AudioClip newClip, string transcript = null)
    {
        if (newClip == null)
        {
            Debug.LogError("VoiceAnalyzer: clip nulo recebido para análise.");
            return;
        }

        if (transcript != null)
            transcriptText = transcript;

        ResetAnalysisState();
        clip = newClip;
        sampleRate = clip.frequency;
        samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        Debug.Log($"▶️ Iniciando análise do clip '{clip.name}' | Duração: {clip.length:F2}s | Samples: {clip.samples} | Channels: {clip.channels}");

        AnalyzeAudio();
        float nota = CalculateScore();

        if (!string.IsNullOrEmpty(transcriptText) && !string.IsNullOrEmpty(openAiApiKey))
        {
            StartCoroutine(GetCoherenceScoreFromOpenAI(transcriptText, nota));
        }
        else if (string.IsNullOrEmpty(transcriptText) && !string.IsNullOrEmpty(openAiApiKey))
        {
            // Sem transcrição? Transcreve com OpenAI e depois aplica a dedução.
            StartCoroutine(TranscribeAndEvaluate(nota));
        }
        else
        {
            Debug.LogWarning($"🎙️ Nota final sem dedução de contexto: {nota:F1}. Motivo: {(string.IsNullOrEmpty(openAiApiKey) ? "chave OpenAI ausente" : "transcrição indisponível")}");
        }
    }

    public void SetTranscript(string transcript) => transcriptText = transcript;

    private void ResetAnalysisState()
    {
        segmentAnalyses = null;
        avgRms = avgDb = avgPitch = 0f;
        activeSpeechSegments = 0;
        speechBursts = 0;
        avgSpeechBurstDuration = 0f;
        avgPauseDuration = 0f;
        excessivePausesCount = 0;
        longestPauseDuration = 0f;
        energyVariationRange = 0f;
        speechContinuityPercent = 0f;
        rmsValue = dbValue = pitchValue = 0f;
        silencePercentage = 0f;
        coherenceScore = 0f; // reinicia score de coerência para nova gravação
    }

    void AnalyzeAudio()
    {
        int segmentSize = (int)(sampleRate * segmentDuration);
        int totalSegments = Mathf.CeilToInt((float)samples.Length / segmentSize);

        segmentAnalyses = new SegmentAnalysis[totalSegments];

        float totalRms = 0f;
        float totalDb = 0f;
        float totalPitch = 0f;
        int silentSegments = 0;
        activeSpeechSegments = 0;

        Debug.Log($"🔍 Analisando {totalSegments} segmentos de {segmentDuration}s cada...");
        if (debugSilence)
            Debug.Log($"[DBG] thresholds: rms<{rmsSilenceThreshold:F5} ou dBFS<{dbFsSilenceThreshold:F1} => silêncio");

        // Analisa cada segmento individualmente
        for (int seg = 0; seg < totalSegments; seg++)
        {
            int startIdx = seg * segmentSize;
            int endIdx = Mathf.Min(startIdx + segmentSize, samples.Length);
            int currentSegmentSize = endIdx - startIdx;

            SegmentAnalysis analysis = new SegmentAnalysis();
            analysis.timeStamp = seg * segmentDuration;

            // Extrai samples do segmento
            float[] segmentSamples = new float[currentSegmentSize];
            Array.Copy(samples, startIdx, segmentSamples, 0, currentSegmentSize);

            // 1. Calcula RMS do segmento
            float sum = 0f;
            for (int i = 0; i < currentSegmentSize; i++)
                sum += segmentSamples[i] * segmentSamples[i];

            analysis.rms = Mathf.Sqrt(sum / currentSegmentSize);

            // 2. Converte RMS em decibéis (escala relativa usada pelo scoring existente)
            analysis.db = 20 * Mathf.Log10(analysis.rms / 0.1f + 1e-5f);

            // 3. dBFS real (para detecção de silêncio mais robusta)
            float dbFs = 20f * Mathf.Log10(Mathf.Max(analysis.rms, 1e-7f));

            // 4. Detecta silêncio (mais tolerante)
            analysis.isSilence = (analysis.rms < rmsSilenceThreshold) || (dbFs < dbFsSilenceThreshold);

            if (debugSilence && (seg < debugLogFirstSegments || (debugLogEvery > 0 && seg % debugLogEvery == 0)))
            {
                Debug.Log($"[DBG seg {seg}] t={analysis.timeStamp:F2}s | rms={analysis.rms:F6} | dBFS={dbFs:F1} | dB(rel)={analysis.db:F1} | silence={analysis.isSilence}");
            }

            if (analysis.isSilence)
            {
                analysis.pitch = 0f;
                silentSegments++;
            }
            else
            {
                // 4. Estima pitch apenas se não for silêncio
                analysis.pitch = EstimatePitchFromSegment(segmentSamples, sampleRate);

                // Acumula para média (apenas segmentos com fala)
                totalRms += analysis.rms;
                totalDb += analysis.db;
                totalPitch += analysis.pitch;
                activeSpeechSegments++;
            }

            segmentAnalyses[seg] = analysis;

            // Debug.Log($"  [{seg}] {analysis.timeStamp:F1}s: RMS={analysis.rms:F4} | dB={analysis.db:F1} | Pitch={analysis.pitch:F0}Hz | Silêncio={analysis.isSilence}");
        }

        // Calcula médias globais (apenas dos segmentos com fala)
        if (activeSpeechSegments > 0)
        {
            avgRms = totalRms / activeSpeechSegments;
            avgDb = totalDb / activeSpeechSegments;
            avgPitch = totalPitch / activeSpeechSegments;
        }
        else
        {
            avgRms = 0f;
            avgDb = -100f;
            avgPitch = 0f;
        }

        // Atualiza valores globais para compatibilidade
        rmsValue = avgRms;
        dbValue = avgDb;
        pitchValue = avgPitch;
        silencePercentage = (float)silentSegments / totalSegments * 100f;
        speechContinuityPercent = 100f - silencePercentage;

        // Calcula métricas de ritmo e pausas
        CalculateRhythmMetrics();

        // Calcula variação de energia
        CalculateEnergyVariation();

        Debug.Log($"📈 Médias: RMS={avgRms:F4} | dB={avgDb:F1} | Pitch={avgPitch:F0}Hz | Silêncio={silencePercentage:F1}%");
        Debug.Log($"🎵 Ritmo: {speechBursts} rajadas | Duração média: {avgSpeechBurstDuration:F1}s | Pausas excessivas: {excessivePausesCount}");
        if (debugSilence)
            Debug.Log($"[DBG] segmentos fala={activeSpeechSegments} | silêncio={silentSegments} | total={totalSegments}");
    }

    float EstimatePitchFromSegment(float[] buffer, float sampleRate)
    {
        int bufferSize = buffer.Length;
        // Para segmentos já filtrados (sem silêncio), calcula pitch diretamente
        float maxCorr = 0f;
        int maxLag = 0;
        int maxSamples = Mathf.Min(bufferSize, (int)(sampleRate * 0.5f));

        for (int lag = 50; lag < 1000 && lag < maxSamples; lag++)
        {
            float corr = 0f;
            for (int i = 0; i < maxSamples - lag; i++)
                corr += buffer[i] * buffer[i + lag];

            if (corr > maxCorr)
            {
                maxCorr = corr;
                maxLag = lag;
            }
        }

        if (maxLag == 0) return 0f;
        return sampleRate / maxLag;
    }
    // Método para ler chave do .env
    private string LoadOpenAIApiKey()
    {
        string envPath = Path.Combine(Application.dataPath, "..", ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                if (line.StartsWith("OPENAI_API_KEY="))
                    return line.Substring("OPENAI_API_KEY=".Length).Trim();
            }
        }
        return null;
    }

    // Corrotina para enviar transcrição para OpenAI e obter score de coerência/nexo
    [Serializable]
    private class ChatMessage { public string role; public string content; }
    [Serializable]
    private class ChatRequest { public string model; public ChatMessage[] messages; public int max_tokens; }
    private System.Collections.IEnumerator GetCoherenceScoreFromOpenAI(string transcript, float notaTecnica)
    {
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.LogError("Chave da OpenAI não encontrada no .env");
            Debug.Log($"⚠️ Contexto não avaliado. Nota final (sem desconto): {notaTecnica:F1}");
            yield break;
        }

        string prompt = "Avalie de 0 a 10 a coerência e o nexo da apresentação abaixo. Considere se há começo, meio e fim sobre o mesmo tema, sem avaliar correção científica. Responda apenas com o número.\n\nApresentação:\n" + transcript;

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiApiKey}");
            // Usa classes serializáveis para garantir inclusão do campo 'model'
            var requestBody = new ChatRequest
            {
                model = "gpt-4o-mini", // modelo recomendado (gpt-3.5-turbo descontinuado)
                messages = new[] { new ChatMessage { role = "user", content = prompt } },
                max_tokens = 10
            };
            string json = JsonUtility.ToJson(requestBody);
            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                // Fallback manual se JsonUtility falhar (ambientes antigos / limitações)
                json = "{\"model\":\"gpt-4o-mini\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeForJson(prompt) + "\"}],\"max_tokens\":10}";
                Debug.LogWarning("JsonUtility falhou em serializar request; usando JSON montado manualmente.");
            }
            Debug.Log("[OpenAI] Request JSON: " + json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var postTask = client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            while (!postTask.IsCompleted) yield return null;

            if (postTask.IsFaulted)
            {
                Debug.LogError("Erro ao enviar requisição para OpenAI: " + postTask.Exception?.GetBaseException().Message);
                Debug.Log($"⚠️ Contexto não avaliado (erro de requisição). Nota final: {notaTecnica:F1}");
                yield break;
            }

            var response = postTask.Result;
            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"Erro HTTP OpenAI: {(int)response.StatusCode} {response.ReasonPhrase}");
                Debug.Log($"⚠️ Contexto não avaliado (status HTTP). Nota final: {notaTecnica:F1}");
                yield break;
            }
            var readTask = response.Content.ReadAsStringAsync();
            while (!readTask.IsCompleted) yield return null;

            if (readTask.IsFaulted)
            {
                Debug.LogError("Erro ao ler resposta da OpenAI: " + readTask.Exception?.GetBaseException().Message);
                Debug.Log($"⚠️ Contexto não avaliado (erro leitura). Nota final: {notaTecnica:F1}");
                yield break;
            }

            string result = readTask.Result;
            Debug.Log($"🧠 Resposta da OpenAI: {result}");
            int idx = result.IndexOf("content");
            bool contextoAvaliado = false;
            if (idx >= 0)
            {
                string sub = result.Substring(idx);
                var digits = System.Text.RegularExpressions.Regex.Match(sub, "[0-9]+(\\.[0-9]+)?");
                if (digits.Success)
                {
                    coherenceScore = float.Parse(digits.Value);
                    contextoAvaliado = true;
                }
            }

            if (contextoAvaliado)
            {
                // Pontuação invertida: contexto (0-10) desconta até 10 da nota técnica.
                float descontoContexto = Mathf.Clamp(10f - coherenceScore, 0f, 10f);
                float notaFinal = Mathf.Clamp(notaTecnica - descontoContexto, 0f, 10f);
                Debug.Log($"🎙️ Nota final (contexto invertido): {notaFinal:F1} | Técnica: {notaTecnica:F1} | Contexto: {coherenceScore:F1} | Desconto: {descontoContexto:F1}");
            }
            else
            {
                Debug.LogWarning($"⚠️ Não foi possível avaliar o contexto (formato inesperado). Nota final sem desconto: {notaTecnica:F1}");
            }
        }
        yield break;
    }

    // Transcreve o áudio atual (clip) via OpenAI Whisper e, se obtiver texto, aplica avaliação de contexto.
    private System.Collections.IEnumerator TranscribeAndEvaluate(float notaTecnica)
    {
        if (clip == null)
        {
            Debug.LogError("Transcrição: clip nulo");
            yield break;
        }

        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Debug.LogError("Transcrição: chave da OpenAI ausente");
            yield break;
        }

        byte[] wavBytes = null;
        try
        {
            wavBytes = EncodeWavFromSamples(samples, clip.channels, clip.frequency);
        }
        catch (Exception e)
        {
            Debug.LogError($"Falha ao codificar WAV: {e.Message}");
            yield break;
        }

        using (var client = new HttpClient())
        using (var form = new MultipartFormDataContent())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);

            var fileContent = new ByteArrayContent(wavBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(fileContent, "file", "audio.wav");
            form.Add(new StringContent("whisper-1"), "model");
            // Idioma fixo: português
            form.Add(new StringContent("pt"), "language");

            var postTask = client.PostAsync("https://api.openai.com/v1/audio/transcriptions", form);
            while (!postTask.IsCompleted) yield return null;

            if (postTask.IsFaulted)
            {
                Debug.LogError("Erro na transcrição (requisição): " + postTask.Exception?.GetBaseException().Message);
                yield break;
            }

            var response = postTask.Result;
            var readTask = response.Content.ReadAsStringAsync();
            while (!readTask.IsCompleted) yield return null;

            if (readTask.IsFaulted)
            {
                Debug.LogError("Erro na transcrição (leitura resposta): " + readTask.Exception?.GetBaseException().Message);
                yield break;
            }

            string json = readTask.Result;
            // Estrutura simples esperada: { "text": "..." }
            TranscriptionResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<TranscriptionResponse>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError("Falha ao parsear transcrição: " + ex.Message + " | JSON bruto=" + json);
            }

            if (parsed == null || string.IsNullOrEmpty(parsed.text))
            {
                Debug.LogWarning("Transcrição vazia ou inválida. Nota final sem dedução de contexto.");
                yield break;
            }

            transcriptText = parsed.text;
            Debug.Log($"📝 Transcrição obtida ({parsed.text.Length} chars)");
            Debug.Log($"📝 {parsed.text}");

            // Agora aplica a avaliação de contexto com a transcrição obtida
            StartCoroutine(GetCoherenceScoreFromOpenAI(transcriptText, notaTecnica));
        }
    }

    [Serializable]
    private class TranscriptionResponse
    {
        public string text;
    }

    // Codifica samples em WAV PCM 16-bit little-endian
    private byte[] EncodeWavFromSamples(float[] floatSamples, int channels, int sampleRate)
    {
        if (floatSamples == null || floatSamples.Length == 0)
            throw new ArgumentException("Samples vazios");

        int bytesPerSample = 2; // 16-bit PCM
        int subchunk2Size = floatSamples.Length * bytesPerSample;
        int chunkSize = 36 + subchunk2Size;

        using (var ms = new MemoryStream(44 + subchunk2Size))
        using (var bw = new BinaryWriter(ms))
        {
            // RIFF header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16); // Subchunk1Size for PCM
            bw.Write((short)1); // AudioFormat = PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            int byteRate = sampleRate * channels * bytesPerSample;
            bw.Write(byteRate);
            short blockAlign = (short)(channels * bytesPerSample);
            bw.Write(blockAlign);
            bw.Write((short)16); // BitsPerSample

            // data chunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(subchunk2Size);

            // PCM data
            for (int i = 0; i < floatSamples.Length; i++)
            {
                float f = Mathf.Clamp(floatSamples[i], -1f, 1f);
                short s = (short)Mathf.RoundToInt(f * 32767f);
                bw.Write(s);
            }

            return ms.ToArray();
        }
    }

    private string EscapeForJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    // (Removidas duplicatas de métodos OpenAI)

    // (Removida versão async duplicada)
    // ...métodos auxiliares continuam acima; duplicata de CalculateScore removida

    float EvaluateVolume()
    {
        // Volume ideal: -25 a -10 dB
        if (avgDb >= -25f && avgDb <= -10f)
            return 1.0f; // Perfeito
        else if (avgDb > -30f && avgDb < -25f)
            return 0.75f; // Um pouco baixo
        else if (avgDb > -10f && avgDb <= -5f)
            return 0.70f; // Um pouco alto
        else if (avgDb > -35f && avgDb <= -30f)
            return 0.5f; // Baixo
        else
            return 0.2f; // Muito inadequado
    }

    float EvaluateClarity()
    {
        // 60% baseado em RMS (energia/articulação)
        float rmsScore = 0f;
        if (avgRms >= 0.015f && avgRms <= 0.08f)
            rmsScore = 0.6f; // Clareza ideal
        else if (avgRms > 0.08f && avgRms <= 0.12f)
            rmsScore = 0.4f; // Um pouco forte
        else if (avgRms > 0.008f && avgRms < 0.015f)
            rmsScore = 0.35f; // Baixa energia
        else
            rmsScore = 0.1f;

        // 40% baseado em consistência de volume
        float consistencyScore = CalculateVolumeConsistency() * 0.4f;

        return rmsScore + consistencyScore;
    }

    float EvaluatePacing()
    {
        float pacingScore = 0f;

        // Avalia número de "rajadas" de fala
        // Para apresentações de 1-5 min: 3-10 rajadas é bom
        float durationMinutes = (segmentAnalyses.Length * segmentDuration) / 60f;
        float expectedBurstsMin = durationMinutes * 1.5f;
        float expectedBurstsMax = durationMinutes * 4f;

        if (speechBursts >= expectedBurstsMin && speechBursts <= expectedBurstsMax)
            pacingScore += 0.4f;
        else if (speechBursts >= expectedBurstsMin * 0.7f)
            pacingScore += 0.2f;

        // Avalia duração média de rajadas (8-25s é ideal)
        if (avgSpeechBurstDuration >= 8f && avgSpeechBurstDuration <= 25f)
            pacingScore += 0.4f;
        else if (avgSpeechBurstDuration >= 5f && avgSpeechBurstDuration < 8f)
            pacingScore += 0.25f; // Um pouco rápido demais
        else if (avgSpeechBurstDuration > 25f && avgSpeechBurstDuration <= 35f)
            pacingScore += 0.25f; // Um pouco longo
        else if (avgSpeechBurstDuration < 5f)
            pacingScore += 0.1f; // Muito picado

        // Avalia pausas médias (0.5-2s é natural)
        if (avgPauseDuration >= 0.5f && avgPauseDuration <= 2.5f)
            pacingScore += 0.2f;
        else if (avgPauseDuration < 0.5f)
            pacingScore += 0.05f; // Fala muito corrida
        else if (avgPauseDuration <= 4f)
            pacingScore += 0.1f; // Pausas um pouco longas

        return pacingScore;
    }

    float EvaluateExpressiveness()
    {
        // 60% baseado em variação de energia (ênfase)
        float energyScore = 0f;
        if (energyVariationRange >= 0.02f && energyVariationRange <= 0.06f)
            energyScore = 0.6f; // Variação ideal (mostra ênfase)
        else if (energyVariationRange >= 0.015f && energyVariationRange <= 0.08f)
            energyScore = 0.45f; // Boa variação
        else if (energyVariationRange < 0.01f)
            energyScore = 0.15f; // Muito monótono
        else if (energyVariationRange > 0.1f)
            energyScore = 0.3f; // Muito variável
        else
            energyScore = 0.25f;

        // 40% baseado em variação de pitch (entonação)
        float pitchScore = CalculatePitchVariationScore() * 0.4f;

        return energyScore + pitchScore;
    }

    float EvaluateContinuity()
    {
        // % ideal de tempo falando: 65-85%
        if (speechContinuityPercent >= 65f && speechContinuityPercent <= 85f)
            return 1.0f; // Ideal
        else if (speechContinuityPercent >= 55f && speechContinuityPercent < 65f)
            return 0.7f; // Um pouco pausado
        else if (speechContinuityPercent > 85f && speechContinuityPercent <= 92f)
            return 0.75f; // Muito corrido
        else if (speechContinuityPercent >= 45f)
            return 0.4f; // Muitas pausas
        else if (speechContinuityPercent > 92f)
            return 0.5f; // Sem pausas
        else{
            Debug.LogWarning($"⚠️ Continuidade crítica: {speechContinuityPercent:F1}% de fala");
            return 0.1f; // Crítico
        }
    }

    void CalculateRhythmMetrics()
    {
        speechBursts = 0;
        excessivePausesCount = 0;
        longestPauseDuration = 0f;

        bool wasInSilence = true;
        int currentBurstLength = 0;
        int currentPauseLength = 0;
        float totalBurstDuration = 0f;
        float totalPauseDuration = 0f;
        int totalPauses = 0;

        foreach (var seg in segmentAnalyses)
        {
            if (!seg.isSilence)
            {
                // Está falando
                if (wasInSilence)
                {
                    // Nova rajada de fala começou
                    speechBursts++;

                    // Registra pausa que acabou
                    if (currentPauseLength > 0)
                    {
                        float pauseDuration = currentPauseLength * segmentDuration;
                        totalPauseDuration += pauseDuration;
                        totalPauses++;

                        if (pauseDuration > longestPauseDuration)
                            longestPauseDuration = pauseDuration;

                        // Pausa excessiva: >10s
                        if (pauseDuration > 10f)
                            excessivePausesCount++;

                        currentPauseLength = 0;
                    }
                }
                currentBurstLength++;
                wasInSilence = false;
            }
            else
            {
                // Está em silêncio
                if (!wasInSilence)
                {
                    // Rajada de fala terminou
                    if (currentBurstLength > 0)
                    {
                        totalBurstDuration += currentBurstLength * segmentDuration;
                        currentBurstLength = 0;
                    }
                }
                currentPauseLength++;
                wasInSilence = true;
            }
        }

        // Finaliza última rajada se estava falando
        if (!wasInSilence && currentBurstLength > 0)
        {
            totalBurstDuration += currentBurstLength * segmentDuration;
        }

        // Finaliza última pausa se estava em silêncio
        if (wasInSilence && currentPauseLength > 0)
        {
            float pauseDuration = currentPauseLength * segmentDuration;
            totalPauseDuration += pauseDuration;
            totalPauses++;

            if (pauseDuration > longestPauseDuration)
                longestPauseDuration = pauseDuration;

            if (pauseDuration > 10f)
                excessivePausesCount++;
        }

        // Calcula médias
        avgSpeechBurstDuration = speechBursts > 0 ? totalBurstDuration / speechBursts : 0f;
        avgPauseDuration = totalPauses > 0 ? totalPauseDuration / totalPauses : 0f;
    }

    void CalculateEnergyVariation()
    {
        if (activeSpeechSegments == 0)
        {
            energyVariationRange = 0f;
            return;
        }

        float maxRms = 0f;
        float minRms = float.MaxValue;

        foreach (var seg in segmentAnalyses)
        {
            if (!seg.isSilence)
            {
                maxRms = Mathf.Max(maxRms, seg.rms);
                minRms = Mathf.Min(minRms, seg.rms);
            }
        }

        energyVariationRange = maxRms - minRms;
    }

    float CalculateVolumeConsistency()
    {
        if (activeSpeechSegments == 0) return 0f;

        float sumSquaredDiff = 0f;
        int count = 0;

        foreach (var seg in segmentAnalyses)
        {
            if (!seg.isSilence)
            {
                float diff = seg.db - avgDb;
                sumSquaredDiff += diff * diff;
                count++;
            }
        }

        if (count == 0) return 0f;

        float stdDev = Mathf.Sqrt(sumSquaredDiff / count);

        // Desvio padrão ideal: < 5 dB (consistente)
        // Retorna score 0-1 baseado em consistência
        if (stdDev < 3f) return 1.0f;
        else if (stdDev < 5f) return 0.8f;
        else if (stdDev < 8f) return 0.5f;
        else return 0.2f;
    }

    float CalculatePitchVariationScore()
    {
        if (activeSpeechSegments == 0) return 0f;

        float sumSquaredDiff = 0f;
        int count = 0;

        foreach (var seg in segmentAnalyses)
        {
            if (!seg.isSilence && seg.pitch > 0)
            {
                float diff = seg.pitch - avgPitch;
                sumSquaredDiff += diff * diff;
                count++;
            }
        }

        if (count == 0) return 0f;

        float stdDev = Mathf.Sqrt(sumSquaredDiff / count);

        // Variação ideal: 15-40 Hz (entonação natural)
        if (stdDev >= 15f && stdDev <= 40f)
            return 1.0f; // Entonação natural
        else if (stdDev >= 10f && stdDev < 15f)
            return 0.7f; // Um pouco monótono
        else if (stdDev > 40f && stdDev <= 60f)
            return 0.7f; // Boa variação
        else if (stdDev < 10f)
            return 0.3f; // Muito monótono
        else
            return 0.5f; // Muito variável
    }

    // (Removido: método legado EstimatePitchInBuffer e comentários duplicados)

    float CalculateScore()
    {
        if (segmentAnalyses == null || segmentAnalyses.Length == 0)
        {
            Debug.LogError("Nenhum segmento analisado!");
            return 0f;
        }

        float score = 0f;

        // ===== 1. AUDIBILIDADE (20% = 2.0 pontos) =====
        float volumeScore = EvaluateVolume();
        score += volumeScore * 2.0f;

        // ===== 2. CLAREZA (20% = 2.0 pontos) =====
        float clarityScore = EvaluateClarity();
        score += clarityScore * 2.0f;

        // ===== 3. RITMO E PAUSAS (20% = 2.0 pontos) =====
        float pacingScore = EvaluatePacing();
        score += pacingScore * 2.0f;

        // ===== 4. EXPRESSIVIDADE (20% = 2.0 pontos) =====
        float expressivenessScore = EvaluateExpressiveness();
        score += expressivenessScore * 2.0f;

        // ===== 5. CONTINUIDADE (20% = 2.0 pontos) =====
        float continuityScore = EvaluateContinuity();
        score += continuityScore * 2.0f;

        // ===== 6. PENALIZAÇÃO POR PAUSAS EXCESSIVAS (>10s) =====
        float excessivePausePenalty = excessivePausesCount * 0.5f; // -0.5 por pausa >10s
        if (excessivePausePenalty > 0)
        {
            score -= excessivePausePenalty;
            Debug.LogWarning($"⚠️ Penalização por {excessivePausesCount} pausas >10s: -{excessivePausePenalty:F1} pontos");
        }

        // Normaliza para escala 0-10
        float finalScore = Mathf.Clamp(score, 0f, 10f);

        Debug.Log($"\n📊 ========== RESULTADO FINAL ==========");
        Debug.Log($"   🎤 AUDIBILIDADE: {volumeScore:F2}/1.0 ({volumeScore * 20:F0}%)");
        Debug.Log($"   🔊 CLAREZA: {clarityScore:F2}/1.0 ({clarityScore * 20:F0}%)");
        Debug.Log($"   ⏱️ RITMO: {pacingScore:F2}/1.0 ({pacingScore * 20:F0}%)");
        Debug.Log($"   🎭 EXPRESSIVIDADE: {expressivenessScore:F2}/1.0 ({expressivenessScore * 20:F0}%)");
        Debug.Log($"   ▶️ CONTINUIDADE: {continuityScore:F2}/1.0 ({continuityScore * 20:F0}%)");
        Debug.Log($"   ⚠️ PENALIZAÇÕES: -{excessivePausePenalty:F1}");
        Debug.Log($"   ⭐ NOTA FINAL: {finalScore:F1}/10");
        Debug.Log($"=======================================");

        // Imprime sugestões de melhoria baseadas nas métricas
        string sugestoes = BuildImprovementSuggestions(volumeScore, clarityScore, pacingScore, expressivenessScore, continuityScore, excessivePausePenalty, finalScore);
        Debug.Log("\n🔧 SUGESTÕES DE MELHORIA:\n" + sugestoes);

        return finalScore;
    }

    string BuildImprovementSuggestions(float volumeScore, float clarityScore, float pacingScore, float expressivenessScore, float continuityScore, float penalties, float finalScore)
    {
        System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();

        // Volume
        if (volumeScore < 0.75f)
        {
            if (avgDb < -30f) list.Add("Aumentar ligeiramente o volume (microfone ou projeção da voz).");
            else if (avgDb > -12f) list.Add("Reduzir um pouco o volume para evitar saturação/perda de conforto.");
            else list.Add("Tornar o volume mais consistente entre os trechos.");
        }

        // Clareza
        if (clarityScore < 0.6f)
        {
            if (avgRms < 0.015f) list.Add("Articular melhor as palavras e elevar um pouco a energia vocal.");
            list.Add("Manter variação de volume estável para facilitar compreensão.");
        }

        // Ritmo / Pausas
        if (pacingScore < 0.6f)
        {
            if (avgPauseDuration < 0.5f) list.Add("Inserir pequenas pausas para dar tempo de processamento ao ouvinte.");
            else if (avgPauseDuration > 4f) list.Add("Reduzir pausas longas para manter o fluxo envolvente.");
            list.Add("Equilibrar duração das rajadas de fala (ideal entre 8–25s).");
        }

        // Expressividade
        if (expressivenessScore < 0.6f)
        {
            if (energyVariationRange < 0.015f) list.Add("Variar mais a energia (ênfase) para evitar monotonia.");
            float pitchVarScore = CalculatePitchVariationScore();
            if (pitchVarScore < 0.7f) list.Add("Explorar entonação (subir e descer levemente o pitch em pontos-chave).");
        }

        // Continuidade
        if (continuityScore < 0.7f)
        {
            if (speechContinuityPercent < 55f) list.Add("Aumentar tempo efetivo de fala reduzindo silencios prolongados.");
            else if (speechContinuityPercent > 85f) list.Add("Inserir pausas curtas estratégicas para dar respiro e reforçar pontos.");
        }

        // Pausas excessivas
        if (penalties > 0f) list.Add("Evitar pausas acima de 10 segundos; retomar antes para manter engajamento.");

        // Resultado geral
        if (finalScore < 7.5f) list.Add("Revisar conteúdo para incrementar clareza e estrutura antes da próxima apresentação.");

        if (list.Count == 0) list.Add("Ótimo desempenho geral! Mantenha consistência e prática.");
        return string.Join("\n - ", list).Insert(0, " - ");
    }
}
