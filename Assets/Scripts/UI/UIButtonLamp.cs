using UnityEngine;
using UnityEngine.UI;

public class UIButtonLamp : MonoBehaviour
{
    [SerializeField] private Image background;

    [Header("Colors")]
    [SerializeField] private Color onColor = Color.white;
    [SerializeField] private Color offColor = Color.gray;

    private void Awake()
    {
        if (!background) background = GetComponent<Image>();
    }

    public void SetOn(bool on)
    {
        if (!background) return;
        background.color = on ? onColor : offColor;
    }

    // aby umożliwić ustawienie kolorów z innego skryptu
    public void SetColors(Color on, Color off)
    {
        onColor = on;
        offColor = off;
    }
}