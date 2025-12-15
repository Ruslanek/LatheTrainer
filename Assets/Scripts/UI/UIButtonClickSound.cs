using UnityEngine;
using UnityEngine.UI;

public class UIButtonClickSound : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button reverseButton;

    [Header("Clips")]
    [SerializeField] private AudioClip clickStart;
    [SerializeField] private AudioClip clickStop;
    [SerializeField] private AudioClip clickReverse;

    private void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();

        if (startButton) startButton.onClick.AddListener(() => Play(clickStart));
        if (stopButton) stopButton.onClick.AddListener(() => Play(clickStop));
        if (reverseButton) reverseButton.onClick.AddListener(() => Play(clickReverse));
    }

    private void Play(AudioClip clip)
    {
        if (!audioSource || !clip) return;
        audioSource.PlayOneShot(clip);
    }
}