using UnityEngine;

namespace LatheTrainer.Machine
{
    /// <summary>
    /// Вращение ChuckRotatingRoot + включение/выключение
    /// метки и кулачков (2 кулачка, метка между ними).
    /// </summary>
    public class ChuckVisualSpinSimple : MonoBehaviour
    {
        [Header("Обороты шпинделя (об/мин)")]
        public float spindleRpm = 300f;

        [Header("Элементы, которые бегают по кругу")]
        public SpriteRenderer keyMark;
        public SpriteRenderer jawTop;
        public SpriteRenderer jawBottom;

        [Header("Настройки видимости")]
        [Tooltip("Градусы вокруг 'переда', в которых элементы видны")]
        public float frontHalfAngle = 90f;
        // видим в секторе ±90° вокруг передней точки

        private float _angleDeg; // общий угол поворота ChuckRotatingRoot

        private void Update()
        {
            RotateRoot();
            UpdateVisibility();
        }

        private void RotateRoot()
        {
            if (Mathf.Approximately(spindleRpm, 0f))
                return;

            // скорость в градусах в секунду
            float degreesPerSecond = Mathf.Abs(spindleRpm) * 360f / 60f;
            float delta = degreesPerSecond * Time.deltaTime * Mathf.Sign(spindleRpm);

            _angleDeg = Mathf.Repeat(_angleDeg + delta, 360f);

            // Вращаем вокруг горизонтальной оси X
            // (как ось шпинделя в боковом виде)
            transform.localRotation = Quaternion.Euler(_angleDeg, 0f, 0f);
        }

        private void UpdateVisibility()
        {
            // 0° считаем "передом" (к нам),
            // 180° — "сзади".
            float normalized = Mathf.DeltaAngle(0f, _angleDeg);

            bool isFront = Mathf.Abs(normalized) <= frontHalfAngle;

            SetVisible(keyMark, isFront);
            SetVisible(jawTop, isFront);
            SetVisible(jawBottom, isFront);
        }

        private void SetVisible(SpriteRenderer r, bool visible)
        {
            if (r != null)
                r.enabled = visible;
        }
    }
}