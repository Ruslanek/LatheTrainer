using UnityEngine;

namespace LatheTrainer.Core
{
    public class ToolPositionController : MonoBehaviour
    {
        [Header("Текущие координаты резца (мм)")]
        public float XMm = 50f;   // радиальное направление
        public float ZMm = 150f;  // вдоль заготовки

        [Header("Скорость перемещения (мм/с)")]
        public float FeedRateMmPerSec = 50f;

        [Header("Ограничения станка (мм)")]
        public LatheLimits Limits = new LatheLimits(
            xMinMm: 0f,    // ось шпинделя
            xMaxMm: 120f,  // максимально вверх
            zMinMm: 0f,    // ближе к патрону
            zMaxMm: 300f   // вправо вдоль заготовки
        );

        private const float Scale = 0.01f; // 100 мм = 1 юнит в Unity

        private void Start()
        {
            // сразу выставляем позицию по текущим X/Z
            UpdateWorldPosition();
        }

        private void Update()
        {
            HandleKeyboardInput();
            ClampToLimits();
            UpdateWorldPosition();
        }

        private void HandleKeyboardInput()
        {
            float dx = 0f;
            float dz = 0f;

            // X — вверх/вниз
            if (Input.GetKey(KeyCode.UpArrow)) dx += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) dx -= 1f;

            // Z — вдоль заготовки
            if (Input.GetKey(KeyCode.RightArrow)) dz += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) dz -= 1f;

            if (dx == 0f && dz == 0f)
                return;

            float dt = Time.deltaTime;

            XMm += dx * FeedRateMmPerSec * dt;
            ZMm += dz * FeedRateMmPerSec * dt;
        }

        private void ClampToLimits()
        {
            XMm = Mathf.Clamp(XMm, Limits.XMinMm, Limits.XMaxMm);
            ZMm = Mathf.Clamp(ZMm, Limits.ZMinMm, Limits.ZMaxMm);
        }

        private void UpdateWorldPosition()
        {
            // Z → по горизонтали, X → по вертикали
            Vector3 worldPos = new Vector3(
                ZMm * Scale,
                XMm * Scale,
                0f
            );

            transform.localPosition = worldPos;
        }
    }
}