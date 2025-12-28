using UnityEngine;
using LatheTrainer.Core;
using LatheTrainer.UI;
using System.Collections;
using UnityEngine.UI;
using LatheTrainer.Machine;


namespace LatheTrainer.Machine
{
    public class AxisMotionController : MonoBehaviour
    {
        public enum Mode { Jog, Rapid }

        [Header("Refs")]
        [SerializeField] private ToolPositionController tool;
        [SerializeField] private SpindleFeedPanel panel;

        [Header("Mode")]
        [SerializeField] private Mode currentMode = Mode.Rapid;

        [Header("Increment (mm)")]
        [SerializeField] private float incrementStepMm = 1f; // 0.1 / 1 / 10


        [Header("HOME target (mm)")]
        [SerializeField] private float homeXmm = -390.7916f;
        [SerializeField] private float homeZmm = 281.9971f;


        [SerializeField] private AxisMotionController motion;

        [Header("Buttons Images")]
        [SerializeField] private Image jogImg;
        [SerializeField] private Image rapidImg;

        [SerializeField] private Image inc01Img;
        [SerializeField] private Image inc1Img;
        [SerializeField] private Image inc10Img;

        [Header("Colors")]
        [SerializeField] private Color active = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color inactive = new Color(1f, 1f, 1f, 1f);

        [SerializeField] private float posEpsMm = 0.2f;

        [Header("SFX")]
        [SerializeField] private AudioSource sfxMove;
        [SerializeField] private float sfxMinSpeedToPlay = 0.01f;


        private bool _isHoming;
        private Coroutine _homeRoutine;

        [SerializeField] private MoveMode currentMoveMode = MoveMode.Continuous;

        public bool IsHoming => _isHoming;

        public Mode CurrentFeedMode => currentMode;
        public MoveMode CurrentMoveMode => currentMoveMode;
        public float CurrentIncrementStepMm => incrementStepMm;

        [Header("SFX Increment")]
        [SerializeField] private float sfxIncrementHoldSec = 0.3f;

        private Coroutine _incSfxRoutine;
        private float _incSfxStopAt;



        public System.Action OnStateChanged;

        private void NotifyState() => OnStateChanged?.Invoke();

        public enum MoveMode { Continuous, Increment }

        public void SetJogMode()
        {
            currentMode = Mode.Jog;
            currentMoveMode = MoveMode.Continuous;          // ✅ przywracamy tryb ruchu ciągłego (przytrzymanie)
            if (panel) panel.SetFeedMode(SpindleFeedPanel.FeedMode.Jog);
            NotifyState();
        }

        public void SetRapidMode()
        {
            currentMode = Mode.Rapid;
            currentMoveMode = MoveMode.Continuous;          // ✅ przywracamy ruch ciągły (przytrzymanie)
            if (panel) panel.SetFeedMode(SpindleFeedPanel.FeedMode.Rapid);
            NotifyState();
        }

        public void SetIncrementStep(float stepMm)
        {
            incrementStepMm = Mathf.Max(0.0001f, stepMm);
            currentMoveMode = MoveMode.Increment;           // ✅ włączamy tryb krokowy
            NotifyState();
        }

        public bool IsIncrementMode() => currentMoveMode == MoveMode.Increment;

        public float GetCurrentSpeedMmPerSec()
        {
            if (!panel) return 0f;

            float speed;

            if (currentMode == Mode.Jog)
            {
                int n = panel.GetCurrentRpm();
                float f = panel.GetJogFeedMmPerRev();

                speed = (n <= 0 || f <= 0f) ? 0f : (f * n) / 60f;
            }
            else
            {
                float v = panel.GetRapidMmPerMin();
                speed = (v <= 0f) ? 0f : v / 60f;
            }

           // Debug.Log($"[AxisMotion] SpeedCalc mode={currentMode} => {speed:0.###} mm/s");
            return speed;
        }

        public bool CanMove() => GetCurrentSpeedMmPerSec() > 0.0001f;

        // Przytrzymanie klawiszy strzałek
        public void StartMove(float dirX, float dirZ)
        {
            if (_isHoming) return;
            if (!tool) return;

           // Debug.Log($"[AxisMotion] dirX={dirX} dirZ={dirZ}  (Up:+X, Right:+Z, Left:-Z)");


            float speed = GetCurrentSpeedMmPerSec();
            //Debug.Log($"[AxisMotion] StartMove dirX={dirX} dirZ={dirZ} mode={currentMode} speed={speed:0.###} mm/s");

            if (sfxMove)
            {
                if (speed > sfxMinSpeedToPlay)
                {
                    if (!sfxMove.isPlaying) sfxMove.Play();
                }
                else
                {
                    if (sfxMove.isPlaying) sfxMove.Stop();
                }
            }


            if (speed <= 0.0001f)
            {
                Debug.Log("[MOVE] Speed=0 -> ruch zablokowany");
                tool.StopMove();
                return;
            }

            tool.StartContinuousMove(dirX, dirZ, speed);
        }

        public void StopMove()
        {

            if (!tool) return;
            tool.StopMove();

            if (sfxMove && sfxMove.isPlaying)
                sfxMove.Stop();
        }

        // Ruch inkrementalny
        public void StepMove(float dirX, float dirZ)
        {
            if (_isHoming) return;
            if (!tool) return;

            float speed = GetCurrentSpeedMmPerSec();
            if (speed <= 0.0001f)
            {
                Debug.Log("[INC] Speed=0 -> ruch inkrementalny zablokowany");
                return;
            }

            //Debug.Log($"[Inc] step={incrementStepMm:0.###}mm dirX={dirX} dirZ={dirZ} mode={currentMode} speed={speed:0.###} mm/s");

            float dx = dirX * incrementStepMm;
            float dz = dirZ * incrementStepMm;

            PlayIncrementMoveSfx(speed);
            tool.StepMove(dx, dz);

            //Debug.Log($"[Inc] done -> X={tool.XMm:0.###} Z={tool.ZMm:0.###}");
           
        }


        public void GoHome()
        {
            if (_homeRoutine != null) StopCoroutine(_homeRoutine);
            _homeRoutine = StartCoroutine(GoHomeRoutine());
        }

        public IEnumerator GoHomeRoutine()
        {
           // Debug.Log("[HOME] GoHomeRoutine()");

           // if (!tool) { Debug.Log("[HOME] tool==null"); yield break; }
            //if (_isHoming) { Debug.Log("[HOME] already homing"); yield break; }

            _isHoming = true;

            SetRapidMode();
            float speed = GetCurrentSpeedMmPerSec();
            if (speed <= 0.0001f)
            {
                //Debug.Log("[HOME] speed=0 -> forbidden");
                _isHoming = false;
                yield break;
            }

            // Debug.Log($"[HOME] Start -> X={homeXmm:0.###} Z={homeZmm:0.###} speed={speed:0.###}");

            // Przemieszczamy oś X
            yield return MoveAxisXTo(homeXmm, speed);
            // Przemieszczamy oś Z
            yield return MoveAxisZTo(homeZmm, speed);

            tool.StopMove();

            if (sfxMove && sfxMove.isPlaying)
                sfxMove.Stop();

            //Debug.Log("[HOME] Done");
            _isHoming = false;
            _homeRoutine = null;
        }

        private IEnumerator MoveAxisXTo(float targetX, float speed)
        {
            while (Mathf.Abs(tool.XMm - targetX) > posEpsMm)
            {
                tool.XMm = Mathf.MoveTowards(tool.XMm, targetX, speed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator MoveAxisZTo(float targetZ, float speed)
        {
            while (Mathf.Abs(tool.ZMm - targetZ) > posEpsMm)
            {
                tool.ZMm = Mathf.MoveTowards(tool.ZMm, targetZ, speed * Time.deltaTime);
                yield return null;
            }
        }

        private void OnEnable()
        {
            if (motion) motion.OnStateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (motion) motion.OnStateChanged -= Refresh;
        }

        private void Refresh()
        {
            if (!motion) return;

            // Feed mode
            if (jogImg) jogImg.color = (motion.CurrentFeedMode == AxisMotionController.Mode.Jog) ? active : inactive;
            if (rapidImg) rapidImg.color = (motion.CurrentFeedMode == AxisMotionController.Mode.Rapid) ? active : inactive;

            // Move mode + increment step highlight
            bool incMode = motion.CurrentMoveMode == AxisMotionController.MoveMode.Increment;

            if (inc01Img) inc01Img.color = (incMode && Mathf.Abs(motion.CurrentIncrementStepMm - 0.1f) < 0.0001f) ? active : inactive;
            if (inc1Img) inc1Img.color = (incMode && Mathf.Abs(motion.CurrentIncrementStepMm - 1f) < 0.0001f) ? active : inactive;
            if (inc10Img) inc10Img.color = (incMode && Mathf.Abs(motion.CurrentIncrementStepMm - 10f) < 0.0001f) ? active : inactive;
        }

        public bool IsAtHome(float eps = 0.5f)
        {
            if (!tool) return false;
            return Mathf.Abs(tool.XMm - homeXmm) <= eps && Mathf.Abs(tool.ZMm - homeZmm) <= eps;
        }

        private void PlayIncrementMoveSfx(float speed)
        {
            if (!sfxMove) return;

            if (speed <= sfxMinSpeedToPlay)
            {
                if (sfxMove.isPlaying) sfxMove.Stop();
                return;
            }

            // wydłużamy „okno” odtwarzania
            _incSfxStopAt = Time.time + sfxIncrementHoldSec;

            if (_incSfxRoutine == null)
                _incSfxRoutine = StartCoroutine(IncrementSfxRoutine());

            if (!sfxMove.isPlaying)
                sfxMove.Play();
        }

        private IEnumerator IncrementSfxRoutine()
        {
            while (Time.time < _incSfxStopAt)
                yield return null;

            if (sfxMove && sfxMove.isPlaying)
                sfxMove.Stop();

            _incSfxRoutine = null;
        }
    }
}
