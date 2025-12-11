using UnityEngine;

namespace LatheTrainer.Machine
{
    /// <summary>
    /// Анимация кулачка патрона:
    /// двигаем по вертикали и чуть наклоняем по X,
    /// видимость/скорость строго зависят от spindleRpm.
    /// Один скрипт можно использовать и для верхнего, и для нижнего кулачка.
    /// </summary>
    public class ChuckJawAnimator : MonoBehaviour
    {
        [Header("Обороты шпинделя (об/мин)")]
        public float spindleRpm = 20f;

        [Header("Геометрия движения")]
        [Tooltip("Y-координата центра патрона (в локальных координатах родителя)")]
        public float centerY = 0f;

        [Tooltip("Половина высоты пути кулачка (обычно половина высоты патрона)")]
        public float offsetY = 0.5f;

        [Header("Наклон по X")]
        [Tooltip("Максимальный угол наклона по оси X (вверху/внизу)")]
        public float tiltAmplitudeX = 45f;

        [Header("Фаза движения")]
        [Tooltip("Для нижнего кулачка включи, чтобы он шёл в противофазе")]
        public bool invertPhase = false;

        [Header("Слои отрисовки")]
        [Tooltip("SpriteRenderer неподвижного корпуса патрона (ChuckStatic)")]
        public SpriteRenderer chuckStaticRenderer;

        [Tooltip("Сдвиг order in layer, когда кулачок спереди")]
        public int frontOrderOffset = 1;

        [Tooltip("Сдвиг order in layer, когда кулачок сзади")]
        public int backOrderOffset = -1;





        private float _angleDeg;      // накопленный угол вращения, градусов
        private float _baseZ;         // базовый угол по Z (как выставишь в инспекторе)
        private SpriteRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            // запоминаем исходный угол по Z, чтобы не портить твой спрайт
            _baseZ = transform.localEulerAngles.z;
        }

        private void Update()
        {
            if (Mathf.Approximately(spindleRpm, 0f))
                return;

            // 1. Угол вращения в градусах (напрямую из RPM)
            float degreesPerSecond = spindleRpm * 360f / 60f;
            _angleDeg = Mathf.Repeat(_angleDeg + degreesPerSecond * Time.deltaTime, 360f);

            float angleRad = _angleDeg * Mathf.Deg2Rad;
            float s = Mathf.Sin(angleRad);
            float c = Mathf.Cos(angleRad);

            if (invertPhase)
                s = -s;    // нижний кулачок пойдёт "наоборот" относительно верхнего

            // 2. Позиция по Y: центр ± offsetY * sin(θ)
            var lp = transform.localPosition;
            lp.y = centerY - offsetY * s;   // если направление кажется "не тем" — поменяй знак
            transform.localPosition = lp;

            // 3. Наклон по X: 0° в центре, tiltAmplitudeX вверху/внизу
            float tiltX = tiltAmplitudeX * Mathf.Abs(s);
            transform.localRotation = Quaternion.Euler(tiltX, 0f, _baseZ);

            /*// 4. Видимость: кулачок виден, когда cos(θ) ≥ 0 (передняя полусфера)
            bool visible = c >= 0f;
            if (_renderer != null)
                _renderer.enabled = visible;

            // 4. Видимость: кулачки всегда видны
            if (_renderer != null)
                _renderer.enabled = true;*/

            // 4. Слои: спереди/сзади относительно ChuckStatic
            if (_renderer != null)
            {
                int baseOrder = 0;
                if (chuckStaticRenderer != null)
                    baseOrder = chuckStaticRenderer.sortingOrder;

                // cos(θ) >= 0 → передняя полусфера
                // cos(θ) <  0 → задняя полусфера
                bool isFront = c >= 0f;

                int targetOrder = baseOrder + (isFront ? frontOrderOffset : backOrderOffset);
                _renderer.sortingOrder = targetOrder;
            }
        }
    }
}