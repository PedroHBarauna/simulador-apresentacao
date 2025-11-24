using System;
using UnityEngine;

/// <summary>
/// Analisador de áudio simples que extrai métricas vocais básicas
/// e atribui uma nota objetiva (0 a 10) com base em clareza, volume e variação tonal.
/// Agora com método público AnalyzeClip para ser chamado após gravação.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceAnalyzer : MonoBehaviour
{
    public AudioClip clip; // ainda pode ser usado no Inspector
    private float[] samples;
    private float sampleRate;
    private float rmsValue;
    private float dbValue;
    private float pitchValue;

    // NÃO executa a análise automaticamente no Start.
    void Start()
    {
        // opcional: se já houver um clip no inspector, analisa.
        if (clip != null)
            AnalyzeClip(clip);
    }

    // Método público para receber o clip gravado em runtime
    public void AnalyzeClip(AudioClip newClip)
    {
        if (newClip == null)
        {
            Debug.LogError("VoiceAnalyzer: clip nulo recebido para análise.");
            return;
        }

        clip = newClip;
        sampleRate = clip.frequency;
        samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        AnalyzeAudio();
        float nota = CalculateScore();
        Debug.Log($"🎙️ Nota final da apresentação: {nota:F1}");
    }

    void AnalyzeAudio()
    {
        // 1. Calcula RMS (Root Mean Square) – mede intensidade média da voz
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        rmsValue = Mathf.Sqrt(sum / samples.Length);

        // 2. Converte RMS em decibéis (escala logarítmica)
        dbValue = 20 * Mathf.Log10(rmsValue / 0.1f + 1e-5f);

        // 3. Estima frequência fundamental (pitch)
        pitchValue = EstimatePitch(samples, sampleRate);
    }

    float EstimatePitch(float[] buffer, float sampleRate)
    {
        int bufferSize = buffer.Length;
        float maxCorr = 0f;
        int maxLag = 0;
        int maxSamples = Mathf.Min(bufferSize, 44100); // máximo 1 segundo de amostra

        for (int lag = 50; lag < 1000; lag++)
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

    float CalculateScore()
    {
        // Critérios básicos (ajustáveis conforme contexto acadêmico)
        float score = 0f;

        // Intensidade ideal entre -20 e -5 dB
        if (dbValue > -30 && dbValue < -5) score += 3f;
        else if (dbValue >= -5 && dbValue <= 0) score += 2f;
        else score += 1f;

        // Tom médio (pitch) em torno de 100–250 Hz é típico de fala clara
        if (pitchValue >= 90 && pitchValue <= 250) score += 3f;
        else if (pitchValue > 250 && pitchValue < 350) score += 2f;
        else score += 1f;

        // RMS (clareza/energia da voz)
        if (rmsValue > 0.02f && rmsValue < 0.1f) score += 3f;
        else score += 1.5f;

        // Normaliza em uma escala de 0–10
        return Mathf.Clamp(score * (10f / 9f), 0f, 10f);
    }
}
