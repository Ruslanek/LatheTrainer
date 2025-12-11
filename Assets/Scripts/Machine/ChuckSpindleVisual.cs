using UnityEngine;

namespace LatheTrainer.Machine
{
    /// <summary>
    /// Визуальная анимация вращения шпинделя:
    /// - несколько меток на патроне
    /// - несколько кулачков
    /// Всё синхронизировано одним углом и одними RPM.
    /// Скрипт вешается на ChuckRotatingRoot.
    /// </summary>
    public class ChuckSpindleVisual : MonoBehaviour
    {
        [Header("Обороты шпинделя (об/мин)")]
        [Tooltip("Желаемые обороты (задаём извне, например из UI)")]
        public float commandedRpm = 0f;

        [Tooltip("Текущие обороты (используются для анимации)")]
        public float currentRpm = 0f;

        [Tooltip("Скорость разгона/торможения, об/мин в секунду")]
        public float rpmAcceleration = 400f;

        // ---------- МЕТКИ ----------

        [System.Serializable]
        public class MarkConfig
        {
            [Tooltip("SpriteRenderer метки")]
            public SpriteRenderer renderer;

            [Tooltip("Половина вертикального хода метки")]
            public float offsetY = 0.5f;

            [Tooltip("Максимальный наклон по X (вверху/внизу)")]
            public float tiltAmplitudeX = 90f;

            [Tooltip("Фазовый сдвиг по вращению (в градусах)")]
            public float phaseOffsetDeg = 0f;

            [HideInInspector] public float baseZ;
            [HideInInspector] public float centerY;
        }

        [Header("Метки (Key Marks)")]
        public MarkConfig[] marks;

        // ---------- КУЛАЧКИ ----------

        [System.Serializable]
        public class JawConfig
        {
            [Tooltip("SpriteRenderer данного кулачка")]
            public SpriteRenderer renderer;

            [Tooltip("Половина высоты пути этого кулачка")]
            public float offsetY = 0.5f;

            [Tooltip("Максимальный наклон по X вверху/внизу")]
            public float tiltAmplitudeX = 45f;

            [Tooltip("Фазовый сдвиг по вращению (в градусах)")]
            public float phaseOffsetDeg = 0f;

            [HideInInspector] public float baseZ;
        }

        [Header("Кулачки")]
        [Tooltip("Y-координата центра патрона (общая для всех кулачков, локальные координаты ChuckRotatingRoot)")]
        public float jawsCenterY = 0f;

        public JawConfig[] jaws;

        [Header("Слои отрисовки кулачков")]
        [Tooltip("SpriteRenderer неподвижного корпуса патрона (ChuckStatic)")]
        public SpriteRenderer chuckStaticRenderer;

        [Tooltip("Сдвиг order in layer, когда кулачок спереди")]
        public int frontOrderOffset = 1;

        [Tooltip("Сдвиг order in layer, когда кулачок сзади")]
        public int backOrderOffset = -1;

        // ---------- ВНУТРЕННЕЕ СОСТОЯНИЕ ----------

        private float _angleDeg; // общий угол вращения, градусов

        private void Awake()
        {
            // Запоминаем базовые данные для меток
            if (marks != null)
            {
                foreach (var mark in marks)
                {
                    if (mark == null || mark.renderer == null)
                        continue;

                    var tr = mark.renderer.transform;
                    mark.centerY = tr.localPosition.y;
                    mark.baseZ = tr.localEulerAngles.z;
                }
            }

            // Запоминаем базовый Z для каждого кулачка
            if (jaws != null)
            {
                foreach (var jaw in jaws)
                {
                    if (jaw == null || jaw.renderer == null)
                        continue;

                    jaw.baseZ = jaw.renderer.transform.localEulerAngles.z;
                }
            }
        }

        private void Update()
        {
            // 0. Плавно тянем текущие обороты к целевым
            currentRpm = Mathf.MoveTowards(
                currentRpm,
                commandedRpm,
                rpmAcceleration * Time.deltaTime);

            if (Mathf.Approximately(currentRpm, 0f))
                return;

            // 1. Общий угол вращения
            float degreesPerSecond = currentRpm * 360f / 60f;
            _angleDeg = Mathf.Repeat(_angleDeg + degreesPerSecond * Time.deltaTime, 360f);

            // базовые sin/cos можно не считать здесь — для наглядности считаем в каждом блоке
            AnimateMarks();
            AnimateJaws();
        }

        // ---------- МЕТКИ ----------

        private void AnimateMarks()
        {
            if (marks == null)
                return;

            foreach (var mark in marks)
            {
                if (mark == null || mark.renderer == null)
                    continue;

                float angleRad = (_angleDeg + mark.phaseOffsetDeg) * Mathf.Deg2Rad;
                float s = Mathf.Sin(angleRad);
                float c = Mathf.Cos(angleRad);

                // позиция по Y: центр ± offset * sin(θ)
                var tr = mark.renderer.transform;
                var lp = tr.localPosition;
                lp.y = mark.centerY - mark.offsetY * s;
                tr.localPosition = lp;

                // наклон по X
                float tiltX = mark.tiltAmplitudeX * Mathf.Abs(s);
                tr.localRotation = Quaternion.Euler(tiltX, 0f, mark.baseZ);

                // видимость: только передняя полусфера
                mark.renderer.enabled = (c >= 0f);
            }
        }

        // ---------- КУЛАЧКИ ----------

        private void AnimateJaws()
        {
            if (jaws == null)
                return;

            int baseOrder = 0;
            if (chuckStaticRenderer != null)
                baseOrder = chuckStaticRenderer.sortingOrder;

            foreach (var jaw in jaws)
            {
                if (jaw == null || jaw.renderer == null)
                    continue;

                float angleRad = (_angleDeg + jaw.phaseOffsetDeg) * Mathf.Deg2Rad;
                float s = Mathf.Sin(angleRad);
                float c = Mathf.Cos(angleRad);

                // позиция по Y
                var tr = jaw.renderer.transform;
                var lp = tr.localPosition;
                lp.y = jawsCenterY - jaw.offsetY * s;
                tr.localPosition = lp;

                // наклон по X
                float tiltX = jaw.tiltAmplitudeX * Mathf.Abs(s);
                tr.localRotation = Quaternion.Euler(tiltX, 0f, jaw.baseZ);

                // порядок отрисовки спереди/сзади
                bool isFront = c >= 0f;
                int targetOrder = baseOrder + (isFront ? frontOrderOffset : backOrderOffset);
                jaw.renderer.sortingOrder = targetOrder;
            }
        }
    }
}