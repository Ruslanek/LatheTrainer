using UnityEngine;

namespace LatheTrainer.Core
{
    public class ToolPositionController : MonoBehaviour
    {
        [Header("Aktualne współrzędne noża (mm)")]
        public float XMm = 50f;   // kierunek radialny
        public float ZMm = 150f;  // wzdłuż detalu

        [Header("Prędkość posuwu (mm/s)")]
        public float FeedRateMmPerSec = 50f;

        [Header("Ograniczenia maszyny (mm)")]
        public LatheLimits Limits = new LatheLimits(
            xMinMm: 0f,    // oś wrzeciona
            xMaxMm: 120f,  // maksymalnie w górę
            zMinMm: 0f,    // bliżej uchwytu
            zMaxMm: 300f   // w prawo wzdłuż detalu
        );

        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;

        private const float Scale = 0.01f; // 100 mm = 1 jednostka w Unity

        private void Start()
        {
            // od razu ustawiamy pozycję według bieżących X/Z
            UpdateWorldPosition();
        }

        private void Update()
        {
            if (_inputEnabled) { 
                HandleKeyboardInput();}
            ClampToLimits();
            UpdateWorldPosition();
        }

        private void HandleKeyboardInput()
        {
            float dx = 0f;
            float dz = 0f;

            // X — góra/dół
            if (Input.GetKey(KeyCode.UpArrow)) dx += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) dx -= 1f;

            // Z — wzdłuż detalu
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
            // Z → poziomo, X → pionowo
            Vector3 worldPos = new Vector3(
                ZMm * Scale,
                XMm * Scale,
                0f
            );

            transform.localPosition = worldPos;
        }


        public void MoveToMm(float xMm, float zMm, bool instant = true)
        {
            XMm = xMm;
            ZMm = zMm;
            ClampToLimits();
            UpdateWorldPosition();
        }

        public void MoveToPark(bool instant = true)
        {
            // bezpieczne parkowanie: w dół i w prawo (dostosuj do swoich potrzeb)
            MoveToMm(xMm: Limits.XMinMm, zMm: Limits.ZMaxMm, instant: true);
        }

        private bool _inputEnabled = true;
    }
}