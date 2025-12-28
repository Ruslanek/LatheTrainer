using UnityEngine;
using TMPro;

namespace LatheTrainer.UI
{
    public class SpindleFeedPanel : MonoBehaviour
    {
        public enum FeedMode { Jog, Rapid }

        [Header("Wskaźniki")]
        public TMP_Text spindleDisplay;
        public TMP_Text feedDisplay;

        [Header("Pokrętła (obrazy)")]
        public RectTransform spindleKnob;
        public RectTransform feedKnob;

        [Header("Zakresy RPM")]
        public int spindleMinRpm = 0;
        public int spindleMaxRpm = 2500;

        [Header("Jog feed (mm/obr)")]
        public float jogFeedMin = 0.0f;
        public float jogFeedMax = 0.5f;

        [Header("Rapid feed (mm/min)")]
        public float rapidMin = 0f;
        public float rapidMax = 10000f;

        [Header("Tryb posuwu (UI/sterowanie)")]
        public FeedMode feedMode = FeedMode.Rapid;

        [Header("Stan wewnętrzny (0..1)")]
        [Range(0f, 1f)] public float spindleValue = 0.3f;
        [Range(0f, 1f)] public float feedValue = 0.3f;

        [Header("Kąt obrotu pokrętła")]
        public float minAngle = -135f;
        public float maxAngle = 135f;

        [Header("Machine link")]
        public LatheTrainer.Machine.ChuckSpindleVisual spindleVisual;

        private int _lastRpm = -1;

        private void Start() => UpdateUI();

        private void Update()
        {
            // Tymczasowe sterowanie klawiszami (jak wcześniej):
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
            int rpm = GetCurrentRpm();

            // Feed wyświetlamy zgodnie z aktualnym trybem
            if (feedMode == FeedMode.Jog)
            {
                float f = GetJogFeedMmPerRev();
                if (feedDisplay != null) feedDisplay.text = f.ToString("0.00"); // mm/obr
            }
            else
            {
                float v = GetRapidMmPerMin();
                if (feedDisplay != null) feedDisplay.text = Mathf.RoundToInt(v).ToString(); // mm/min
            }

            if (spindleDisplay != null)
                spindleDisplay.text = rpm.ToString("0000");

            // obrót pokręteł
            float spindleAngle = Mathf.Lerp(minAngle, maxAngle, spindleValue);
            float feedAngle = Mathf.Lerp(minAngle, maxAngle, feedValue);

            if (spindleKnob != null) spindleKnob.localEulerAngles = new Vector3(0f, 0f, spindleAngle);
            if (feedKnob != null) feedKnob.localEulerAngles = new Vector3(0f, 0f, feedAngle);

            // wysyłamy wartość RPM do wrzeciona
            if (spindleVisual != null && rpm != _lastRpm)
            {
                spindleVisual.SetCommandedRpm(rpm);
                _lastRpm = rpm;
            }
        }

        public int GetCurrentRpm()
            => Mathf.RoundToInt(Mathf.Lerp(spindleMinRpm, spindleMaxRpm, spindleValue));

        public float GetJogFeedMmPerRev()
            => Mathf.Lerp(jogFeedMin, jogFeedMax, feedValue);

        public float GetRapidMmPerMin()
            => Mathf.Lerp(rapidMin, rapidMax, feedValue);

        public void SetFeedMode(FeedMode mode)
        {
            feedMode = mode;
            UpdateUI();
        }
    }
}