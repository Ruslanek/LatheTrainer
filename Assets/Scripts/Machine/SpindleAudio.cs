using UnityEngine;

public class SpindleAudio : MonoBehaviour
{
    [SerializeField] private LatheTrainer.Machine.ChuckSpindleVisual spindle;
    [SerializeField] private AudioSource motorSource;

    [Header("Motor clip")]
    [SerializeField] private AudioClip motorLoop;

    [Header("RPM mapping")]
    [SerializeField] private float minAudibleRpm = 1f;
    [SerializeField] private float maxRpmForAudio = 2000f;


    [Header("Pitch/Volume")]
    [SerializeField] private float pitchAtMin = 0.75f;
    [SerializeField] private float pitchAtMax = 1.5f;
    [SerializeField] private float volumeAtMin = 0.03f;
    [SerializeField] private float volumeAtMax = 0.9f;

    [Header("Smoothing")]
    [SerializeField] private float pitchSmooth = 8f;
    [SerializeField] private float volumeSmooth = 8f;

    private void Awake()
    {
        if (!spindle) spindle = GetComponent<LatheTrainer.Machine.ChuckSpindleVisual>();
        if (!motorSource) motorSource = GetComponent<AudioSource>();

        if (motorSource && motorLoop)
        {
            motorSource.clip = motorLoop;
            motorSource.loop = true;
            motorSource.playOnAwake = false;
        }
    }

    private void Update()
    {
        if (!spindle || !motorSource || !motorSource.clip) return;

        float rpm = spindle.CurrentRpm;

        // zawsze utrzymujemy pętlę dźwięku aktywną, aby uniknąć kliknięć Play/Stop
        if (!motorSource.isPlaying)
            motorSource.Play();

        // normalizujemy wartość RPM do zakresu 0..1 (zaczynając niemal od zera)
        float t = Mathf.InverseLerp(minAudibleRpm, maxRpmForAudio, Mathf.Max(rpm, 0f));

        float targetPitch = Mathf.Lerp(pitchAtMin, pitchAtMax, t);

        // WAŻNE: przy RPM = 0 ustawiamy głośność na 0, w przeciwnym razie pojawi się „szum tła
        float targetVolume;
        if (rpm <= 0.01f) targetVolume = 0f;
        else targetVolume = Mathf.Lerp(volumeAtMin, volumeAtMax, t);

        motorSource.pitch = Mathf.Lerp(motorSource.pitch, targetPitch, pitchSmooth * Time.deltaTime);
        motorSource.volume = Mathf.Lerp(motorSource.volume, targetVolume, volumeSmooth * Time.deltaTime);
    }
}