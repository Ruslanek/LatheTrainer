using UnityEngine;

namespace LatheTrainer.Core
{
    public class ToolPositionController : MonoBehaviour
    {
        [Header("Aktualne współrzędne noża (mm)")]
        public float XMm = 50f;
        public float ZMm = 150f;

        [Header("Ograniczenia maszyny (mm)")]
        public LatheLimits Limits = new LatheLimits(
            xMinMm: -400f,
            xMaxMm: 120f,
            zMinMm: -400f,
            zMaxMm: 700f
        );

        private float _dirX, _dirZ, _speed;
        //private bool _moving;

        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;

        private const float Scale = 0.01f;

        // Ruch ciągły (przytrzymanie przycisku)
        private bool _moving;
        private Vector2 _moveDir;     // (dx, dz) -1..1
        private float _speedMmPerSec; // mm/s

        private bool _inputEnabled = true;

        private void Start() => UpdateWorldPosition();

        private void Update()
        {

            float dt = Time.deltaTime;

            if (_moving)
            {
                XMm += _dirX * _speed * dt;
                ZMm += _dirZ * _speed * dt;
            }

            if (_inputEnabled)
            {
                //  HandleKeyboardInput(); // tylko do testów / sterowanie ręczne
            }

            ClampToLimits();
            UpdateWorldPosition();

            /*
            if (_inputEnabled && _moving)
            {
                float dt = Time.deltaTime;
                XMm += _moveDir.x * _speedMmPerSec * dt;
                ZMm += _moveDir.y * _speedMmPerSec * dt;

                ClampToLimits();
                UpdateWorldPosition();
            }*/
        }

        public void StartContinuousMove(float dirX, float dirZ, float speedMmPerSec)
        {
            _dirX = dirX;
            _dirZ = dirZ;
            _speed = speedMmPerSec;
            _moving = true;

            /*
            _moveDir = new Vector2(dirX, dirZ);
            _speedMmPerSec = Mathf.Max(0f, speedMmPerSec);
            _moving = (_speedMmPerSec > 0.0001f) && (_moveDir.sqrMagnitude > 0.0001f);*/
        }

        public void StopMove()
        {
            _moving = false;
            _dirX = _dirZ = _speed = 0f;
        }


        public void StepMove(float deltaXmm, float deltaZmm)
        {
            XMm += deltaXmm;
            ZMm += deltaZmm;
            ClampToLimits();
            UpdateWorldPosition();
        }

        private void ClampToLimits()
        {
            XMm = Mathf.Clamp(XMm, Limits.XMinMm, Limits.XMaxMm);
            ZMm = Mathf.Clamp(ZMm, Limits.ZMinMm, Limits.ZMaxMm);
        }

        private void UpdateWorldPosition()
        {
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

        public void ApplyDeltaMm(float dXMm, float dZMm)
        {
            float oldX = XMm;
            float oldZ = ZMm;

            XMm += dXMm;
            ZMm += dZMm;

            ClampToLimits();
            UpdateWorldPosition();

            Debug.Log($"[ToolPos] ΔX={dXMm:0.###} ΔZ={dZMm:0.###} | X:{oldX:0.###}->{XMm:0.###}  Z:{oldZ:0.###}->{ZMm:0.###}");
        }




    }
}