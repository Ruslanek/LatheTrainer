using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using LatheTrainer.Core;


public class LatheButtonsUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LatheTrainer.Machine.ChuckSpindleVisual spindle;

    [Header("UI Lamps")]
    [SerializeField] private UIButtonLamp startLamp;
    [SerializeField] private UIButtonLamp stopLamp;
    [SerializeField] private UIButtonLamp reverseLamp;

    [Header("UI Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button reverseButton;

    [Header("Lamp colors")]
    [SerializeField] private Color startOn = Color.green;
    [SerializeField] private Color startOff = new Color(0.15f, 0.15f, 0.15f);

    [SerializeField] private Color stopOn = Color.red;
    [SerializeField] private Color stopOff = new Color(0.15f, 0.15f, 0.15f);

    [SerializeField] private Color reverseOn = Color.yellow;
    [SerializeField] private Color reverseOff = new Color(0.15f, 0.15f, 0.15f);

    [Header("Blink settings")]
    [SerializeField] private int blinkCount = 3;
    [SerializeField] private float blinkInterval = 0.12f;

    [Header("Reverse wait")]
    [SerializeField] private float waitStopRpmThreshold = 0.5f;

    private bool reverseOnState;
    private Coroutine _reverseRoutine;
    private Coroutine _blinkRoutine;


    [Header("Start blink (spool up/down)")]
    [SerializeField] private float rpmBlinkThreshold = 5f;   // o ile wartość CurrentRpm może różnić się od CommandedRpm
    [SerializeField] private float startBlinkPeriod = 0.25f; // prędkość migania


    private float _blinkTimer;
    private bool _startBlinkState;
    private bool _forceStartBlink; // używane podczas migania sygnalizującego błąd

    [Header("Stop blink (deceleration)")]
    //[SerializeField] private float stopBlinkPeriod = 0.25f;
   // [SerializeField] private float stopSpinThreshold = 1.0f; // dopóki currentRpm > 1 — uznajemy, że wrzeciono nadal się
    private float _stopBlinkTimer;
    private bool _stopBlinkState;

    [Header("Deceleration blink (Start)")]
    [SerializeField] private float decelBlinkPeriod = 0.25f;
    [SerializeField] private float decelSpinThreshold = 1.0f; // dopóki CurrentRpm > 1 — uznajemy, że wrzeciono nadal się obraca
    private float _decelBlinkTimer;
    private bool _decelBlinkState;



    private void Awake()
    {
        // podpięcie zdarzeń
        if (startButton != null) startButton.onClick.AddListener(OnStart);
        if (stopButton != null) stopButton.onClick.AddListener(OnStop);
        if (reverseButton != null) reverseButton.onClick.AddListener(OnReverseToggle);



        // przypisujemy unikalne kolory do każdej „lampki”
        if (startLamp) startLamp.SetColors(startOn, startOff);
        if (stopLamp) stopLamp.SetColors(stopOn, stopOff);
        if (reverseLamp) reverseLamp.SetColors(reverseOn, reverseOff);

        ApplyLamps();
    }

    private void OnStart()
    {
        if (LatheSafetyLock.IsLocked) return;
        if (!spindle) return;

        bool started = spindle.TryStartSpindle();
        if (!started)
        {
            // 🔥 miganie przycisku START, gdy uruchomienie jest niemożliwe (detal nie jest zaciśnięty / trwa zaciskanie)
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = StartCoroutine(BlinkLamp(startLamp, blinkCount, blinkInterval));
        }

        ApplyLamps();
    }

    private void OnStop()
    {
        if (LatheSafetyLock.IsLocked) return;
        if (!spindle) return;

        spindle.StopSpindle();
        ApplyLamps();
    }

    private void OnReverseToggle()
    {
        if (LatheSafetyLock.IsLocked) return;
        if (!spindle) return;

        // jeżeli przełączanie jest już w toku — ignorujemy kolejne naciśnięcie
        if (_reverseRoutine != null) return;

        _reverseRoutine = StartCoroutine(ReverseToggleRoutine());
    }


    private IEnumerator ReverseToggleRoutine()
    {
        // 1) Jeżeli wrzeciono jest włączone — zatrzymujemy je (jakby naciśnięto STOP)
        if (spindle.SpindleEnabled)
        {
            spindle.StopSpindle();
            ApplyLamps();
        }

        // 2) Czekamy, aż wrzeciono faktycznie się zatrzyma
        while (spindle.CurrentRpm > waitStopRpmThreshold)
            yield return null;

        // 3) Przełączamy kierunek obrotów
        reverseOnState = !reverseOnState;
        spindle.SetSpinDirection(reverseOnState);

        ApplyLamps();

        _reverseRoutine = null;
    }

    private IEnumerator BlinkLamp(UIButtonLamp lamp, int times, float interval)
    {
        _forceStartBlink = true;

        for (int i = 0; i < times; i++)
        {
            lamp.SetOn(true);
            yield return new WaitForSeconds(interval);
            lamp.SetOn(false);
            yield return new WaitForSeconds(interval);
        }

        _forceStartBlink = false;
        ApplyLamps();
    }




    private void ApplyLamps()
    {
        bool running = spindle && spindle.SpindleEnabled;

        // STOP zawsze pokazuje tryb pracy (włączony, gdy START jest wyłączony)
        if (stopLamp) stopLamp.SetOn(!running);

        // Reverse
        if (reverseLamp) reverseLamp.SetOn(reverseOnState);

        // START:
        // - jeżeli nie działa -> wyłączamy (a metoda Update będzie migać podczas hamowania, jeśli RPM przekracza próg)
        if (startLamp) startLamp.SetOn(running);
    }

    private void Update()
    {
        if (LatheTrainer.UI.CrashPopupUI.Instance != null && LatheTrainer.UI.CrashPopupUI.Instance.IsOpen)
            return;


        if (!spindle || !startLamp) return;

        // jeżeli trwa miganie sygnalizujące błąd uruchomienia — nie ingerujemy
        if (_forceStartBlink) return;

        // ====== A) START włączony: rozpędzanie / dostrajanie ======
        if (spindle.SpindleEnabled)
        {
            // jeżeli zadana wartość wynosi 0 — lampka świeci światłem ciągłym
            if (spindle.CommandedRpm <= 0.01f)
            {
                startLamp.SetOn(true);
                return;
            }

            float diff = Mathf.Abs(spindle.CommandedRpm - spindle.CurrentRpm);
            bool shouldBlink = diff > rpmBlinkThreshold;

            if (shouldBlink)
            {
                _blinkTimer += Time.deltaTime;
                if (_blinkTimer >= startBlinkPeriod)
                {
                    _blinkTimer = 0f;
                    _startBlinkState = !_startBlinkState;
                    startLamp.SetOn(_startBlinkState);
                }
            }
            else
            {
                _blinkTimer = 0f;
                _startBlinkState = true;
                startLamp.SetOn(true);
            }

            return;
        }

        // ====== B) START wyłączony: hamowanie ======
        // W tym miejscu migamy lampką START, dopóki wrzeciono faktycznie się nie zatrzyma
        if (spindle.CurrentRpm > decelSpinThreshold)
        {
            _decelBlinkTimer += Time.deltaTime;
            if (_decelBlinkTimer >= decelBlinkPeriod)
            {
                _decelBlinkTimer = 0f;
                _decelBlinkState = !_decelBlinkState;
                startLamp.SetOn(_decelBlinkState); // ✅ miga lampka START
            }
        }
        else
        {
            // zatrzymany: lampka START jest zgaszona
            _decelBlinkTimer = 0f;
            _decelBlinkState = false;
            startLamp.SetOn(false);
        }
    }

    public void PressStopExternal()
    {
        OnStop(); // wywołuje spindle.StopSpindle() oraz ApplyLamps()

    }

    public void RefreshLampsExternal()
    {
        ApplyLamps();
    }


}