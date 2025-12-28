using UnityEngine;

using System.Collections;


namespace LatheTrainer.Machine
{
    public class ChuckSpindleVisual : MonoBehaviour
    {
        public enum ChuckState
        {
            Idle_NoWorkpiece,
            Idle_Opened,
            Clamping,
            Clamped,
            Spinning


        }

      

        [Header("DEBUG")]
        [SerializeField] private ChuckState debugState;

        [Header("Referencje")]
        [SerializeField] private Transform chuckRoot;      // główny root uchwytu
        // [SerializeField] private Transform chuckCenter;  // pusty obiekt – środek uchwytu (wymagany)
        [SerializeField] private Transform workpieceRoot;  // root detalu (do debugowania, opcjonalnie)

        [Header("Idle visuals (static)")]
        [SerializeField] private SpriteRenderer idleJawTop;
        [SerializeField] private SpriteRenderer idleJawBottom;
        [SerializeField] private SpriteRenderer idleCenterMark;

        [Header("Spin visuals (3 jaws + 3 marks)")]
        [SerializeField] private SpriteRenderer[] spinJaws;
        [SerializeField] private SpriteRenderer[] spinMarks;

        [Header("Clamping")]
        [SerializeField] private float clampDuration = 1.0f;
       // [SerializeField] private float jawClearance = 0.02f; // niewielki luz

        [Header("RPM")]
        [SerializeField] private float rpmAcceleration = 400f;
        [SerializeField] private float commandedRpm;
        [SerializeField] private float currentRpm;

        public enum SpinDirection { CW = 1, CCW = -1,  }   // CW/CCW można zamienić miejscami, jeśli zajdzie taka potrzeba
        [SerializeField] private SpinDirection spinDirection = SpinDirection.CW;
        public SpinDirection CurrentSpinDirection => spinDirection;

        // public enum SpinDirection { CW = 1, CCW = -1 }
        // [SerializeField] private SpinDirection spinDirection = SpinDirection.CW;


        [Header("Spin Phases")]
        [SerializeField] private float[] jawPhases = { 0f, 120f, 240f };
        [SerializeField] private float[] markPhases = { 60f, 180f, 300f };

        [SerializeField] private SpriteRenderer workpieceSprite;

        private Transform JawSpace => idleJawTop ? idleJawTop.transform.parent : transform;

        private bool _spinBasesCaptured;
        private float[] _spinJawBaseX;
        private float[] _spinMarkBaseX;

        [SerializeField] private int frontOrderOffset = 1;
        [SerializeField] private int backOrderOffset = -1;
        [SerializeField] private SpriteRenderer chuckStaticRenderer; // jeśli chcesz ustawić kolejność rysowania względem korpusu

        private float _workpieceRadiusLocal;

        [SerializeField] private float spinJawRadiusAdd = 0.0f;   // można 0
       // [SerializeField] private float spinMarkRadiusAdd = 0.2f;  // znaczniki trochę dalej (do regulacji)


        [SerializeField] private bool debugSpin = true;
        private int _spinDebugEveryNFrames = 30;

        private float _spinCenterLocalY;
        private bool _spinCenterCaptured;


        [Header("Spin radii")]
      // [SerializeField] private float markRadiusFactor = 1.0f; // dostrojenie w razie potrzeby
       // [SerializeField] private float jawRadiusFactor = 1.0f;  // można pozostawić 1

        private float _chuckRadiusLocal; // promień uchwytu w lokalnej przestrzeni JawSpace


        public float CurrentRpm => currentRpm;
        public float CommandedRpm => commandedRpm;

        // ---- state ----
        public ChuckState State { get; private set; } = ChuckState.Idle_NoWorkpiece;

        // ---- internal ----
        private Coroutine _clampRoutine;
        private float _angleDeg;

        // bazowe pozycje w stanie bezczynności (do powrotu)
        private bool _idleBasesCaptured;
        private float _idleTopBaseY;
        private float _idleBottomBaseY;
        private float _idleMarkBaseY;

        //  docelowe przesunięcia szczęk (oś Y względem pozycji bazowej)
        private float _openOffset;
        private float _clampedOffset;

        // promień detalu w jednostkach (na potrzeby obliczeń zacisku)
        private float _workpieceRadius;

        private float[] _spinMarkBaseZ;

        [SerializeField] private float markTiltXMax = 90f; // 90 = całkowicie „krawędzią”

        [Header("Spindle enable (START/STOP)")]
        [SerializeField] private bool spindleEnabled; // włączany przyciskiem START
        public bool SpindleEnabled => spindleEnabled;

        public bool IsClamped => (State == ChuckState.Clamped || State == ChuckState.Spinning);

        [Header("Default RPM on start")]
        [SerializeField] private float defaultCommandedRpm = 250f;

        void Awake()
        {
           //Debug.Log("ChuckSpindleVisual: Awake");
           // Debug.Log($"[Chuck] chuckRoot ref = {(chuckRoot ? chuckRoot.name : "NULL")}, lossyScale={(chuckRoot ? chuckRoot.lossyScale.ToString() : "-")}");
           // Debug.Log($"[Chuck] idleJawTop parent = {(idleJawTop ? idleJawTop.transform.parent.name : "NULL")}");

            CaptureIdleBases();
           // DumpSceneInfo();

            EnterState(ChuckState.Idle_NoWorkpiece);
        }

        void Update()
        {
            // 1) zezwolenie na uruchomienie obrotów
           // bool canSpin = (State == ChuckState.Clamped || State == ChuckState.Spinning);

            // commandedRpm — nie zmieniamy! To „żądanie” użytkownika
           // float targetRpm = canSpin ? commandedRpm : 0f;


            bool canSpin = spindleEnabled && (State == ChuckState.Clamped || State == ChuckState.Spinning);
            float targetRpm = canSpin ? commandedRpm : 0f;

            // podczas zaciskania zawsze 0
            if (State == ChuckState.Clamping)
                targetRpm = 0f;

            // 2) płynna zmiana aktualnych obrotów
            currentRpm = Mathf.MoveTowards(currentRpm, targetRpm, rpmAcceleration * Time.deltaTime);

            // 3) histereza
            const float SpinOnRpm = 1.0f;
            const float SpinOffRpm = 0.5f;

            if (currentRpm > SpinOnRpm)
            {
                if (State != ChuckState.Spinning)
                    EnterState(ChuckState.Spinning);

                AnimateSpin();
            }
            else if (currentRpm < SpinOffRpm)
            {
                if (State == ChuckState.Spinning)
                    EnterState(ChuckState.Clamped);
            }
            else
            {
                // strefa histerezy
                if (State == ChuckState.Spinning)
                    AnimateSpin();
            }
        }

        // ========================= PUBLIC API =========================

        // wywoływane z zewnątrz po wybraniu lub zamocowaniu detalu
        public void SelectWorkpieceByDiameter(float diameterWorld, float clearanceWorld = 0f)
        {
           // Debug.Log($"[Chuck] SelectWorkpieceByDiameter: diameterWorld={diameterWorld:F4}, clearanceWorld={clearanceWorld:F4}");

            float gapOpen = 2f * diameterWorld;               // 200% średnicy
            float gapClamped = diameterWorld + clearanceWorld;

           // Debug.Log($"[Chuck] -> gapOpenWorld={gapOpen:F4} (want 2D), gapClampedWorld={gapClamped:F4} (want D+clr)");

            SelectWorkpieceByGap(gapOpen, gapClamped);
        }

        public void SelectWorkpieceByGap(float gapOpenWorld, float gapClampedWorld)
        {
            float gapOpenLocal = WorldToJawLocalY(gapOpenWorld);
            float gapClampLocal = WorldToJawLocalY(gapClampedWorld);

            _workpieceRadiusLocal = gapClampLocal * 0.5f; // promień detalu w lokalnym układzie współrzędnych JawSpace

            float jawHalf = GetJawHalfHeightLocal();

            _openOffset = gapOpenLocal * 0.5f + jawHalf;
            _clampedOffset = gapClampLocal * 0.5f + jawHalf;

            float gapClampCheck = 2f * (_clampedOffset - jawHalf);
           // Debug.Log($"[Chuck] gapClampLocal={gapClampLocal:F4} check={gapClampCheck:F4}");

           // Debug.Log($"[Chuck] jawHalfLocal={jawHalf:F4} openOffset={_openOffset:F4} clampedOffset={_clampedOffset:F4}");

            UpdateChuckRadiusFromRenderer();

            EnterState(ChuckState.Idle_Opened);
            StartClamp();
        
        
        }

        // zaślepka (na razie nie zmieniamy skali, aby niczego nie zepsuć)
        public void SetChuckDiameterUnits(float chuckDiameterUnits)
        {
            Debug.Log($"SetChuckDiameterUnits({chuckDiameterUnits}) - TODO");
        }

        // wywoływane z zewnątrz (interfejs użytkownika / logika maszyny)
        public void SetCommandedRpm(float rpm)
        {
            commandedRpm = Mathf.Max(0f, rpm);
            //Debug.Log($"[Chuck] SetCommandedRpm -> {commandedRpm:F1}, state={State}");
        }

        // ========================= STATES =========================

        private void EnterState(ChuckState newState)
        {
           // Debug.Log($"EnterState CALLED: {newState}");
            if (State == newState) return;

            if (State == ChuckState.Clamping && newState != ChuckState.Clamping && _clampRoutine != null)
            {
                StopCoroutine(_clampRoutine);
                _clampRoutine = null;
            }

            if (State == ChuckState.Spinning && newState != ChuckState.Spinning)
                _spinCenterCaptured = false;

            State = newState;
            debugState = newState;

            //Debug.Log($"ChuckSpindleVisual STATE -> {newState}");

            switch (State)
            {
                case ChuckState.Idle_NoWorkpiece:
                    ShowIdle(true);
                    ShowSpin(false);
                    ApplyJawDistanceFromCenter(0.5f);
                    break;

                case ChuckState.Idle_Opened:
                    ShowIdle(true);
                    ShowSpin(false);
                    ApplyJawDistanceFromCenter(_openOffset);
                    break;

                case ChuckState.Clamping:
                    ShowIdle(true);
                    ShowSpin(false);
                    break;

                case ChuckState.Clamped:
                    ShowIdle(true);
                    ShowSpin(false);
                    ApplyJawDistanceFromCenter(_clampedOffset);
                    break;

                case ChuckState.Spinning:
                    ShowIdle(false);
                    ShowSpin(true);

                    _spinCenterCaptured = false;   
                    CaptureSpinCenter();
                    UpdateChuckRadiusFromRenderer();
                    break;
            }
        }

          private void EnforceRpmRules()
          {
              // nie można uruchomić obrotów, dopóki detal nie jest zaciśnięty
              bool canSpin = (State == ChuckState.Clamped || State == ChuckState.Spinning);

              if (!canSpin)
                  commandedRpm = 0f;

              if (State == ChuckState.Clamping)
              {
                  commandedRpm = 0f;
                  currentRpm = 0f;
              }
          }

        // ========================= IDLE / CLAMP =========================

        private void StartClamp()
        {
            if (_clampRoutine != null)
            {
                StopCoroutine(_clampRoutine);
                _clampRoutine = null;
            }

            _clampRoutine = StartCoroutine(ClampRoutine());
        }

        private IEnumerator ClampRoutine()
        {
            EnterState(ChuckState.Clamping);

            float start = _openOffset;
            float target = _clampedOffset;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, clampDuration);
                float k = Mathf.Clamp01(t);

               // ApplyIdleOffset(Mathf.Lerp(start, target, k));
                ApplyJawDistanceFromCenter(Mathf.Lerp(start, target, k));
                yield return null;
            }

            EnterState(ChuckState.Clamped);
        }

        private void CaptureIdleBases()
        {
            if (_idleBasesCaptured) return;

            if (idleJawTop) _idleTopBaseY = idleJawTop.transform.localPosition.y;
            if (idleJawBottom) _idleBottomBaseY = idleJawBottom.transform.localPosition.y;
            if (idleCenterMark) _idleMarkBaseY = idleCenterMark.transform.localPosition.y;

            _idleBasesCaptured = true;
        }

        private void ApplyIdleOffset(float offset)
        {
            CaptureIdleBases();

            if (idleJawTop)
            {
                var p = idleJawTop.transform.localPosition;
                p.y = _idleTopBaseY + offset;
                idleJawTop.transform.localPosition = p;
            }

            if (idleJawBottom)
            {
                var p = idleJawBottom.transform.localPosition;
                p.y = _idleBottomBaseY - offset;
                idleJawBottom.transform.localPosition = p;
            }

            if (idleCenterMark)
            {
                var p = idleCenterMark.transform.localPosition;
                p.y = _idleMarkBaseY;
                idleCenterMark.transform.localPosition = p;
            }
        }

        private void ShowIdle(bool on)
        {
            if (idleJawTop) idleJawTop.enabled = on;
            if (idleJawBottom) idleJawBottom.enabled = on;
            if (idleCenterMark) idleCenterMark.enabled = on;
        }

        // ========================= SPIN =========================

        private void ShowSpin(bool on)
        {
            if (spinJaws != null)
                foreach (var r in spinJaws) if (r) r.enabled = on;

            if (spinMarks != null)
                foreach (var r in spinMarks) if (r) r.enabled = on;
        }

        private void AnimateSpin()
        {
            CaptureSpinBases();
           // UpdateChuckRadiusFromRenderer();

           // float degreesPerSecond = currentRpm * 360f / 60f;
            //_angleDeg = Mathf.Repeat(_angleDeg + degreesPerSecond * Time.deltaTime, 360f);

            float dir = (float)spinDirection; // -1 или +1
            float degreesPerSecond = currentRpm * 360f / 60f;
            _angleDeg = Mathf.Repeat(_angleDeg + dir * degreesPerSecond * Time.deltaTime, 360f);

            //float centerY = GetCenterLocalY();
            float centerY = _spinCenterLocalY;

            float jawRadius = _workpieceRadiusLocal + spinJawRadiusAdd;
            // float markRadius = _workpieceRadiusLocal + spinMarkRadiusAdd;

            //  float jawRadius = _clampedOffset * jawRadiusFactor;             // szczęki — można pozostawić bez zmian
            float markRadius = _chuckRadiusLocal;         // ✅ znaczniki względem uchwytu

            int baseOrder = (chuckStaticRenderer != null) ? chuckStaticRenderer.sortingOrder : 0;

            bool doLog = debugSpin && (Time.frameCount % _spinDebugEveryNFrames == 0);

            /*
            if (doLog)
            {
                // WAŻNE: sprawdzamy, w jakich przestrzeniach pracujemy
                var jawSpace = JawSpace;
                Debug.Log(
                    $"[SpinDBG] frame={Time.frameCount} rpm={currentRpm:F2} ang={_angleDeg:F1} dps={degreesPerSecond:F1}\n" +
                    $"[SpinDBG] centerY(local)={centerY:F4} jawRadius={jawRadius:F4} markRadius={markRadius:F4} wpRadiusLocal={_workpieceRadiusLocal:F4}\n" +
                    $"[SpinDBG] JawSpace={jawSpace.name} jawSpace.lossyScale={jawSpace.lossyScale} posW={jawSpace.position}\n" +
                    $"[SpinDBG] idleCenterMark parent={(idleCenterMark ? idleCenterMark.transform.parent.name : "NULL")} localPos={(idleCenterMark ? idleCenterMark.transform.localPosition.ToString() : "-")}\n" +
                    $"[SpinDBG] spinJaw0 parent={(spinJaws != null && spinJaws.Length > 0 && spinJaws[0] ? spinJaws[0].transform.parent.name : "NULL")} localPos={(spinJaws != null && spinJaws.Length > 0 && spinJaws[0] ? spinJaws[0].transform.localPosition.ToString() : "-")} worldPos={(spinJaws != null && spinJaws.Length > 0 && spinJaws[0] ? spinJaws[0].transform.position.ToString() : "-")}"
                );
            }*/

            // --- 3 szczęki ---
            for (int i = 0; i < spinJaws.Length; i++)
            {
                var r = spinJaws[i];
                if (!r) continue;

                float phase = (jawPhases != null && i < jawPhases.Length) ? jawPhases[i] : 0f;
                float ang = (_angleDeg + phase) * Mathf.Deg2Rad;

                float s = Mathf.Sin(ang);
                float c = Mathf.Cos(ang);

                var tr = r.transform;
                var p = tr.localPosition;

                float newY = centerY + jawRadius * s;

                p.x = _spinJawBaseX[i];
                p.y = newY;
                tr.localPosition = p;

                bool isFront = c >= 0f;
                r.sortingOrder = baseOrder + (isFront ? frontOrderOffset : backOrderOffset);
                r.enabled = true;

                /*
                if (doLog)
                {
                    Debug.Log(
                        $"[SpinDBG] Jaw{i}: baseX={_spinJawBaseX[i]:F4} s={s:F3} c={c:F3} -> localY={newY:F4} localPos={tr.localPosition} worldPos={tr.position} parent={tr.parent.name}"
                    );
                }*/
            }

            // --- 3 znaczniki ---
            for (int i = 0; i < spinMarks.Length; i++)
            {
                var r = spinMarks[i];
                if (!r) continue;

                float phase = (markPhases != null && i < markPhases.Length) ? markPhases[i] : 0f;
                float ang = (_angleDeg + phase) * Mathf.Deg2Rad;

                float s = Mathf.Sin(ang);
                float c = Mathf.Cos(ang);

                var tr = r.transform;
                var p = tr.localPosition;

                float newY = centerY + markRadius * s;

                p.x = _spinMarkBaseX[i];
                p.y = newY;
                tr.localPosition = p;

                // 1) „nachylenie” wokół osi X: góra/dół → 90°, środek → 0°
                float tiltX = markTiltXMax * Mathf.Abs(s);
                tr.localRotation = Quaternion.Euler(tiltX, 0f, _spinMarkBaseZ[i]);

                // 2) przód/tył jak wcześniej
                bool isFront = c >= 0f;
                r.enabled = isFront;
                r.sortingOrder = baseOrder + (isFront ? frontOrderOffset : backOrderOffset);

                /*
                if (doLog)
                {
                    Debug.Log($"[SpinDBG] Mark{i}: s={s:F3} c={c:F3} tiltX={tiltX:F1} enabled={r.enabled}");
                }*/
            }
        }

        // ========================= DEBUG =========================

        
        private void DumpSceneInfo()
        {
            Debug.Log("=== ChuckSpindleVisual: Scene dump ===");

            if (chuckRoot)
            {
                Debug.Log($"chuckRoot localPos={chuckRoot.localPosition} worldPos={chuckRoot.position} localScale={chuckRoot.localScale}");
            }
            else Debug.LogWarning("chuckRoot is NULL");

           

            DumpSprite("idleJawTop", idleJawTop);
            DumpSprite("idleJawBottom", idleJawBottom);
            DumpSprite("idleCenterMark", idleCenterMark);

            if (workpieceRoot)
            {
                Debug.Log($"workpieceRoot localPos={workpieceRoot.localPosition} worldPos={workpieceRoot.position} localScale={workpieceRoot.localScale}");
                var wpSr = workpieceRoot.GetComponentInChildren<SpriteRenderer>();
                if (wpSr) DumpSprite("workpieceSprite", wpSr);
            }
        }

       
        private void DumpSprite(string name, SpriteRenderer sr)
        {
            if (!sr)
            {
                Debug.LogWarning($"{name}: NULL");
                return;
            }

            var b = sr.bounds; // WORLD bounds
            Debug.Log($"{name}: localPos={sr.transform.localPosition} worldPos={sr.transform.position} " +
                      $"localScale={sr.transform.localScale} bounds.size(world)={b.size}");
        }

        private float GetCenterLocalY()
        {
            if (idleCenterMark != null)
                return idleCenterMark.transform.localPosition.y;

            if (idleJawTop != null && idleJawBottom != null)
                return (idleJawTop.transform.localPosition.y + idleJawBottom.transform.localPosition.y) * 0.5f;

            return 0f;
        }

        private void ApplyJawDistanceFromCenter(float distance)
        {
            float centerY = GetCenterLocalY();

            if (idleJawTop)
            {
                var p = idleJawTop.transform.localPosition;
                p.y = centerY + distance;
                idleJawTop.transform.localPosition = p;
            }

            if (idleJawBottom)
            {
                var p = idleJawBottom.transform.localPosition;
                p.y = centerY - distance;
                idleJawBottom.transform.localPosition = p;
            }

            // znacznik zawsze w centrum
            if (idleCenterMark)
            {
                var p = idleCenterMark.transform.localPosition;
                p.y = centerY;
                idleCenterMark.transform.localPosition = p;
            }
        }

        private float WorldToChuckLocalY(float worldDistance)
        {
            if (!chuckRoot) return worldDistance;

            Vector3 baseW = chuckRoot.position;
            Vector3 p0w = baseW;
            Vector3 p1w = baseW + Vector3.up * worldDistance;

            Vector3 p0l = chuckRoot.InverseTransformPoint(p0w);
            Vector3 p1l = chuckRoot.InverseTransformPoint(p1w);

            float local = Mathf.Abs(p1l.y - p0l.y);

            Debug.Log($"[Chuck][W2L] root={chuckRoot.name} worldDist={worldDistance:F4} => localDist={local:F4} " +
                      $"rootLossyScale={chuckRoot.lossyScale}");

            return local;
        }

        private float GetJawHalfHeightLocal()
        {
            if (!idleJawTop) return 0f;

            var sr = idleJawTop;                 // pobieramy górną szczękę w stanie idle
            var b = sr.bounds;                   // granice WORLD szczęki

            Transform space = JawSpace;

            // przeliczamy górę i dół granic (bounds) do lokalnej przestrzeni
            Vector3 topL = space.InverseTransformPoint(new Vector3(0f, b.max.y, 0f));
            Vector3 botL = space.InverseTransformPoint(new Vector3(0f, b.min.y, 0f));

            float heightLocal = Mathf.Abs(topL.y - botL.y);
            return heightLocal * 0.5f;
        }

      
        private float WorldToJawLocalY(float worldDistance)
        {
            Transform space = JawSpace;
            Vector3 baseW = space.position;
            Vector3 p0w = baseW;
            Vector3 p1w = baseW + Vector3.up * worldDistance;

            Vector3 p0l = space.InverseTransformPoint(p0w);
            Vector3 p1l = space.InverseTransformPoint(p1w);

            float local = Mathf.Abs(p1l.y - p0l.y);

           // Debug.Log($"[Chuck][W2L] space={space.name} worldDist={worldDistance:F4} => localDist={local:F4} lossyScale={space.lossyScale}");

            return local;
        }

        private void CaptureSpinBases()
        {
            if (_spinBasesCaptured) return;

            if (spinJaws != null)
            {
                _spinJawBaseX = new float[spinJaws.Length];
                for (int i = 0; i < spinJaws.Length; i++)
                    if (spinJaws[i]) _spinJawBaseX[i] = spinJaws[i].transform.localPosition.x;
            }

            if (spinMarks != null)
            {
                _spinMarkBaseX = new float[spinMarks.Length];
                _spinMarkBaseZ = new float[spinMarks.Length];

                for (int i = 0; i < spinMarks.Length; i++)
                {
                    if (!spinMarks[i]) continue;
                    _spinMarkBaseX[i] = spinMarks[i].transform.localPosition.x;
                    _spinMarkBaseZ[i] = spinMarks[i].transform.localEulerAngles.z;
                }
            }

            _spinBasesCaptured = true;
        }

        private void CaptureSpinCenter()
        {
            if (_spinCenterCaptured) return;

            Transform spinSpace = spinJaws != null && spinJaws.Length > 0
                ? spinJaws[0].transform.parent
                : transform;

            // pobieramy środek z idleCenterMark (TO JEST POPRAWNE)
            if (idleCenterMark != null)
            {
                Vector3 worldCenter = idleCenterMark.transform.position;
                Vector3 localCenter = spinSpace.InverseTransformPoint(worldCenter);

                _spinCenterLocalY = localCenter.y;

               // Debug.Log($"[SpinCenter] captured from idleCenterMark world={worldCenter} localY={_spinCenterLocalY:F4}");
            }
            else
            {
                _spinCenterLocalY = 0f;
               // Debug.LogWarning("[SpinCenter] idleCenterMark missing, fallback to 0");
            }

            _spinCenterCaptured = true;
        }

        private void UpdateChuckRadiusFromRenderer()
        {
            if (chuckStaticRenderer == null)
            {
               // Debug.LogWarning("[Chuck] chuckStaticRenderer is NULL -> can't compute chuck radius");
                return;
            }

            // 1) promień uchwytu w przestrzeni WORLD w osi Y (średnica w pionie)
            float chuckRadiusWorld = chuckStaticRenderer.bounds.size.y * 0.5f;

            // 2) przeliczenie do lokalnej przestrzeni JawSpace
            _chuckRadiusLocal = WorldToJawLocalY(chuckRadiusWorld);

            //Debug.Log($"[Chuck] chuckRadiusWorld={chuckRadiusWorld:F4} -> chuckRadiusLocal={_chuckRadiusLocal:F4} (JawSpace={JawSpace.name})");
        }

        public void SetSpinDirection(bool reverse)
        {
            spinDirection = reverse ? SpinDirection.CW : SpinDirection.CCW;
            //Debug.Log($"[Chuck] SpinDirection -> {spinDirection}");
        }

        void Start()
        {
            TryAutoClampOnStart();

            if (commandedRpm <= 0.01f)
                commandedRpm = defaultCommandedRpm;
        }

        private void TryAutoClampOnStart()
        {
            if (workpieceRoot == null)
            {
               // Debug.Log("[Chuck] No workpieceRoot on start → skip auto clamp");
                return;
            }

            var wpRenderer = workpieceRoot.GetComponentInChildren<SpriteRenderer>();
            if (wpRenderer == null)
            {
                Debug.LogWarning("[Chuck] Workpiece has no SpriteRenderer → skip auto clamp");
                return;
            }

            // rzeczywista średnica detalu w przestrzeni WORLD
            float diameterWorld = wpRenderer.bounds.size.y;

            //Debug.Log($"[Chuck] Auto clamp on start: diameterWorld={diameterWorld:F4}");

            //  WAŻNE: używamy TEJ SAMEJ logiki co przy przycisku OK
            SelectWorkpieceByDiameter(diameterWorld);
        }

        public bool TryStartSpindle()
        {
            if (!IsClamped) return false;   // nie można, dopóki detal nie jest zaciśnięty
            spindleEnabled = true;
            return true;
        }

        public void StopSpindle()
        {
            spindleEnabled = false;
        }

    }
}