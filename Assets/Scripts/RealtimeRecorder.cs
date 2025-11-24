using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class RealtimeRecorder : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.Space;
    public Button toggleButton; // opcional: atribuir no Inspector
    public int maxRecordSeconds = 300; // tempo máximo de gravação (segundos)
    public int sampleRate = 44100; // taxa de amostragem desejada
    public VoiceAnalyzer voiceAnalyzer; // arraste o componente no Inspector (ou deixe nulo para buscar)
    public bool autoSendToAnalyzer = true; // se true, chamará AnalyzeClip após salvar

    private string microphoneDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private int channels = 1;

    void Start()
    {
        // escolhe o dispositivo padrão se houver mais de um
        if (Microphone.devices.Length > 0)
            microphoneDevice = Microphone.devices[0];
        else
            microphoneDevice = null; // Microphone.Start aceita null como default

        // se o botão de UI estiver configurado, adiciona o listener
        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleRecording);

        // tenta buscar o VoiceAnalyzer automaticamente se não foi setado
        if (voiceAnalyzer == null)
            voiceAnalyzer = GetComponent<VoiceAnalyzer>();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleRecording();
        }
    }

    public void ToggleRecording()
    {
        if (!isRecording)
            StartRecording();
        else
            StopRecordingAndSave();
    }

    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("Já está gravando.");
            return;
        }

        // Inicia gravação. Usamos loop = true para sempre termos dados; vamos cortar ao parar.
        recordingClip = Microphone.Start(microphoneDevice, true, maxRecordSeconds, sampleRate);
        if (recordingClip == null)
        {
            Debug.LogError("Falha ao iniciar Microphone.");
            return;
        }

        channels = recordingClip.channels;
        isRecording = true;
        Debug.Log("Gravação iniciada.");
    }

    public void StopRecordingAndSave()
    {
        if (!isRecording)
        {
            Debug.LogWarning("Não está gravando.");
            return;
        }

        int pos = Microphone.GetPosition(microphoneDevice);
        Microphone.End(microphoneDevice);
        isRecording = false;

        if (pos <= 0)
        {
            Debug.LogWarning("Nenhuma amostra gravada.");
            return;
        }

        // Extrai amostras relevantes
        float[] samples = new float[pos * channels];
        recordingClip.GetData(samples, 0);

        // Cria novo AudioClip com o tamanho exato
        AudioClip trimmedClip = AudioClip.Create("Recording_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), pos, channels, recordingClip.frequency, false);
        trimmedClip.SetData(samples, 0);

        // Salva como WAV
        string filename = $"recording_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.wav";
        string path = Path.Combine(Application.persistentDataPath, filename);
        SaveWav(trimmedClip, path);

        Debug.Log($"Gravação salva em: {path}");

        // Chama o analyzer se configurado
        if (autoSendToAnalyzer && voiceAnalyzer != null)
        {
            voiceAnalyzer.AnalyzeClip(trimmedClip);
            Debug.Log("Enviado para VoiceAnalyzer.");
        }
    }

    // Função para salvar WAV (16-bit PCM)
    public static void SaveWav(AudioClip clip, string filepath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filepath));

        using (var fileStream = new FileStream(filepath, FileMode.Create))
        {
            int channels = clip.channels;
            int sampleRate = clip.frequency;
            float[] samples = new float[clip.samples * channels];
            clip.GetData(samples, 0);

            // Convert float[] samples to 16-bit PCM
            Int16[] intData = new Int16[samples.Length];
            Byte[] bytesData = new Byte[samples.Length * 2];

            const float rescaleFactor = 32767f; // to convert float to Int16

            for (int i = 0; i < samples.Length; i++)
            {
                float f = Mathf.Clamp(samples[i], -1f, 1f);
                intData[i] = (short)(f * rescaleFactor);
                byte[] byteArr = BitConverter.GetBytes(intData[i]);
                bytesData[i * 2] = byteArr[0];
                bytesData[i * 2 + 1] = byteArr[1];
            }

            // WAV header
            // ChunkID "RIFF"
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
            fileStream.Write(BitConverter.GetBytes(36 + bytesData.Length), 0, 4); // ChunkSize
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);
            // Subchunk1ID "fmt "
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
            fileStream.Write(BitConverter.GetBytes(16), 0, 4); // Subchunk1Size for PCM
            fileStream.Write(BitConverter.GetBytes((short)1), 0, 2); // AudioFormat = 1 (PCM)
            fileStream.Write(BitConverter.GetBytes((short)channels), 0, 2); // NumChannels
            fileStream.Write(BitConverter.GetBytes(sampleRate), 0, 4); // SampleRate
            int byteRate = sampleRate * channels * 2; // SampleRate * NumChannels * BitsPerSample/8
            fileStream.Write(BitConverter.GetBytes(byteRate), 0, 4);
            short blockAlign = (short)(channels * 2);
            fileStream.Write(BitConverter.GetBytes(blockAlign), 0, 2);
            short bitsPerSample = 16;
            fileStream.Write(BitConverter.GetBytes(bitsPerSample), 0, 2);
            // Subchunk2ID "data"
            fileStream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
            fileStream.Write(BitConverter.GetBytes(bytesData.Length), 0, 4);
            // Data
            fileStream.Write(bytesData, 0, bytesData.Length);
        }
    }
}
