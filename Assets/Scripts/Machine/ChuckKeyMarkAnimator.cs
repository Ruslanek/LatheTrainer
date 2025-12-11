using UnityEngine;

namespace LatheTrainer.Machine
{
    /// <summary>
    /// Анимация метки на патроне:
    /// двигаем её по вертикали и наклоняем по X,
    /// время движения строго зависит от spindleRpm.
    /// Угол по Z (ромб 45°) сохраняется.
    /// </summary>
    public class ChuckKeyMarkAnimator : MonoBehaviour
    {
        [Header("Обороты шпинделя (об/мин)")]
        public float spindleRpm = 200f;

        [Header("Амплитуда по вертикали")]
        [Tooltip("Половина высоты пути метки (обычно половина высоты патрона)")]
        public float offsetY = 0.5f;

        private float _centerY;      // стартовая позиция (центр)
        private float _angleDeg;     // накопленный угол, градусов
        private float _baseZ;        // базовый угол по Z (например, 45°)
        private SpriteRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _centerY = transform.localPosition.y;
            _baseZ = transform.localEulerAngles.z; // запоминаем твой ромб
        }

        private void Update()
        {
            if (Mathf.Approximately(spindleRpm, 0f))
                return;

            // 1. Считаем угол вращения в градусах (с учётом знака RPM)
            float degreesPerSecond = spindleRpm * 360f / 60f;
            _angleDeg = Mathf.Repeat(_angleDeg + degreesPerSecond * Time.deltaTime, 360f);

            // Переводим в радианы
            float angleRad = _angleDeg * Mathf.Deg2Rad;
            float s = Mathf.Sin(angleRad);
            float c = Mathf.Cos(angleRad);

            // 2. Позиция по Y: центр ± offsetY * sin(θ)
            var lp = transform.localPosition;
            lp.y = _centerY - offsetY * s;    // знак можно поменять, если "едет не туда"
            transform.localPosition = lp;

            // 3. Наклон по X: 0° в центре, 90° вверху/внизу
            float tiltX = 90f * Mathf.Abs(s);  // |sin(θ)|: 0 в центре, 1 на краях
            transform.localRotation = Quaternion.Euler(tiltX, 0f, _baseZ);

            // 4. Видимость: метка видна, когда cos(θ) ≥ 0 (передняя полусфера)
            bool visible = c >= 0f;
            if (_renderer != null)
                _renderer.enabled = visible;
        }
    }
}