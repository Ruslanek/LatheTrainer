using UnityEngine;
using UnityEngine.UI;
using TMPro; // если используешь TextMeshPro

namespace LatheTrainer.UI
{
    public class SpindleFeedPanel : MonoBehaviour
    {
        [Header("Индикаторы")]
        public TMP_Text spindleDisplay;
        public TMP_Text feedDisplay;

        [Header("Крутилки (изображения)")]
        public RectTransform spindleKnob;
        public RectTransform feedKnob;

        [Header("Диапазоны значений")]
        public int spindleMinRpm = 100;
        public int spindleMaxRpm = 2000;

        public float feedMin = 0.05f;   // мм/об
        public float feedMax = 0.5f;

        [Header("Внутреннее состояние (0..1)")]
        [Range(0f, 1f)] public float spindleValue = 0.3f; // позиция ручки
        [Range(0f, 1f)] public float feedValue = 0.3f;

        [Header("Угол поворота ручки")]
        public float minAngle = -135f;
        public float maxAngle = 135f;

        private void Start()
        {
            UpdateUI();
        }

        private void Update()
        {
            // ВРЕМЕННОЕ управление с клавиатуры,
            // потом заменим на управление мышью:
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
            // вычисляем реальные значения
            int rpm = Mathf.RoundToInt(Mathf.Lerp(spindleMinRpm, spindleMaxRpm, spindleValue));
            float feed = Mathf.Lerp(feedMin, feedMax, feedValue);

            // обновляем текстовые индикаторы
            if (spindleDisplay != null)
                spindleDisplay.text = rpm.ToString("0000");  // 4 цифры

            if (feedDisplay != null)
                feedDisplay.text = feed.ToString("0.00");    // 0.00 мм/об

            // поворачиваем ручки
            float spindleAngle = Mathf.Lerp(minAngle, maxAngle, spindleValue);
            float feedAngle = Mathf.Lerp(minAngle, maxAngle, feedValue);

            if (spindleKnob != null)
                spindleKnob.localEulerAngles = new Vector3(0f, 0f, spindleAngle);

            if (feedKnob != null)
                feedKnob.localEulerAngles = new Vector3(0f, 0f, feedAngle);
        }

        // На будущее: сюда можно добавить методы GetCurrentRpm(), GetCurrentFeed()
        public int GetCurrentRpm() => Mathf.RoundToInt(Mathf.Lerp(spindleMinRpm, spindleMaxRpm, spindleValue));
        public float GetCurrentFeed() => Mathf.Lerp(feedMin, feedMax, feedValue);
    }
}
