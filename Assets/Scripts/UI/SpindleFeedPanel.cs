using UnityEngine;
using UnityEngine.UI;
using TMPro; 

namespace LatheTrainer.UI
{
    public class SpindleFeedPanel : MonoBehaviour
    {
        [Header("Wskaźniki")]
        public TMP_Text spindleDisplay;
        public TMP_Text feedDisplay;

        [Header("Pokrętła (obrazy)")]
        public RectTransform spindleKnob;
        public RectTransform feedKnob;

        [Header("Zakresy wartości")]
        public int spindleMinRpm = 100;
        public int spindleMaxRpm = 2000;

        public float feedMin = 0.05f;   // mm na obrót
        public float feedMax = 0.5f;

        [Header("Stan wewnętrzny (0..1)")]
        [Range(0f, 1f)] public float spindleValue = 0.3f; // pozycja pokrętła
        [Range(0f, 1f)] public float feedValue = 0.3f;

        [Header("Kąt obrotu pokrętła")]
        public float minAngle = -135f;
        public float maxAngle = 135f;

        private void Start()
        {
            UpdateUI();
        }

        private void Update()
        {
            // TYMCZASOWE sterowanie z klawiatury,
            // później zastąpimy sterowaniem myszą:
            if (Input.GetKey(KeyCode.Q))
                spindleValue = Mathf.Clamp01(spindleValue + Time.deltaTime * 0.2f);
            if (Input.GetKey(KeyCode.A))
                spindleValue = Mathf.Clamp01(spindleValue - Time.deltaTime * 0.2f);

            if (Input.GetKey(KeyCode.W))
                feedValue = Mathf.Clamp01(feedValue + Time.deltaTime * 0.2f);
            if (Input.GetKey(KeyCode.S))
                feedValue = Mathf.Clamp01(feedValue - Time.deltaTime * 0.2f);

            UpdateUI();
        }

        private void UpdateUI()
        {
            // obliczamy rzeczywiste wartości
            int rpm = Mathf.RoundToInt(Mathf.Lerp(spindleMinRpm, spindleMaxRpm, spindleValue));
            float feed = Mathf.Lerp(feedMin, feedMax, feedValue);

            // aktualizujemy wskaźniki tekstowe
            if (spindleDisplay != null)
                spindleDisplay.text = rpm.ToString("0000");  // 4 cyfry

            if (feedDisplay != null)
                feedDisplay.text = feed.ToString("0.00");    // 0.00 mm/obr

            // obracamy pokrętła
            float spindleAngle = Mathf.Lerp(minAngle, maxAngle, spindleValue);
            float feedAngle = Mathf.Lerp(minAngle, maxAngle, feedValue);

            if (spindleKnob != null)
                spindleKnob.localEulerAngles = new Vector3(0f, 0f, spindleAngle);

            if (feedKnob != null)
                feedKnob.localEulerAngles = new Vector3(0f, 0f, feedAngle);
        }

        // Na przyszłość: tutaj można dodać metody GetCurrentRpm(), GetCurrentFeed()
        public int GetCurrentRpm() => Mathf.RoundToInt(Mathf.Lerp(spindleMinRpm, spindleMaxRpm, spindleValue));
        public float GetCurrentFeed() => Mathf.Lerp(feedMin, feedMax, feedValue);
    }
}
