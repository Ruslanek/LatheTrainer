using LatheTrainer.Core;
using LatheTrainer.UI;
using SFB;
using System;
using System.Collections;
using System.IO;
using System.Net.NetworkInformation;
using UnityEngine;
using static LatheTrainer.Machine.ChuckSpindleVisual;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using SFB;
#endif


namespace LatheTrainer.Machine
{
    public class WorkpieceMachiningTexture : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private SpriteRenderer workpieceRenderer;
        [SerializeField] private ToolPositionController toolPosition;
        [SerializeField] private ChuckSpindleVisual spindle;
        [SerializeField] private AxisMotionController axisMotion;
        [SerializeField] private LatheButtonsUI latheButtonsUI;
        [SerializeField] private Collider2D workpieceCollider; // przypisz kolider detalu

        [Header("Tool tip (auto by tag)")]
        [SerializeField] private string toolTipTag = "ToolTip";
        [SerializeField] private float refindInterval = 0.25f;

        private Transform toolTip;
        private Collider2D _toolTipCollider;   // kolider części skrawającej (ToolTip)
        private Collider2D _toolBodyCollider;  // (stare) jeśli miałeś jeden — zostawiam, ale realnie używasz tablicy
        private ToolCutProfile _toolProfile;

        [Header("Cut rules")]
        [SerializeField] private float minRpmToCut = 5f;

        [Header("Visual")]
        [SerializeField] private Color machinedFillColorDefault = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private int machinedFillThicknessPxDefault = 4;

        [Header("Edge soften")]
        [SerializeField] private bool softenEdge = true;
        [Range(0, 255)]
        [SerializeField] private byte softenAlphaAdd = 40;

        [Header("Edge paint")]
        [SerializeField] private Color machinedEdgeColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private int edgeThicknessPx = 2;

        public enum CutMode { ProfileCarve, LatheRevolve }
        [SerializeField] private CutMode cutMode = CutMode.LatheRevolve;
        [SerializeField] private bool mirrorCutByRotation = true;

        [Header("Machined column paint")]
        [SerializeField] private bool paintWholeColumnOnTouch = true;
        [SerializeField] private Color machinedColumnColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        [Header("Helix / threading effect")]
        [SerializeField] private bool enableHelix = true;
        [Range(0.01f, 1f)]
        [SerializeField] private float helixDuty = 0.2f;
        [SerializeField] private float minPitchMmPerRev = 0.001f;

        [Header("Units")]
        [SerializeField] private float unitsPerMm = 0.018f;

        // ====== Safety / Crash ======
        [Header("Crash / Safety")]
        [SerializeField] private bool enableCrashChecks = true;
        [SerializeField] private float rpmZeroThreshold = 0.1f;
        [SerializeField] private float rapidContactLimitSec = 0.07f;
        [SerializeField] private float retractMm = 50f;
        [SerializeField] private float crashCooldown = 0.35f;





        private float _rapidContactTimer;
        private float _nextCrashTime;

        // ====== Runtime texture ======
        private Texture2D _tex;
        private Color32[] _pixels;
        private Color32[] _basePixels;

        private int _texW, _texH;          // ✅ rzeczywiste rozmiary tekstury/tablicy
        private int _centerY;
        private Bounds _spriteLocalBounds;
        private float _spindlePhase01;

        private float _refindTimer;

        // ===== Chuck / jaws collision (always on) =====
        [Header("Crash: Chuck / Jaws (always ON)")]
        [SerializeField] private bool enableChuckChecks = true;
        [SerializeField] private string chuckHazardTag = "ChuckHazard";
        [SerializeField] private string toolBodyTag = "ToolBody";
        [SerializeField] private float chuckRefindInterval = 0.25f;

        private Collider2D[] _chuckHazards;
        private Collider2D[] _toolBodyColliders;

        [SerializeField] private LatheStateController latheState;

        //==========================
        [Header("Parting off")]
        [SerializeField] private bool enablePartingDetect = true;
        [SerializeField] private float partingCheckCooldown = 0.08f;
        // [SerializeField] private float emptyColumnAlphaThreshold = 2; //  <= 2 traktujemy jako puste

        [SerializeField] private Transform cutPartSpawnRoot;
        [SerializeField] private PartDropAnimator partDropAnimator;
        [SerializeField] private DrawingUI drawingUI;

        private float _nextPartingCheckTime;
        private bool _isPartedOff;


        [Header("Crash refine by pixels")]
        [SerializeField] private bool refineCrashByPixels = true;
        [SerializeField] private int crashPixelStep = 2;              // 1..4 zazwyczaj poprawne
        [SerializeField] private byte crashAlphaThreshold = 5;        // określa, co uznajemy za „materiał”

        public enum MachineAxisUnity { X, Y }
        [SerializeField] private MachineAxisUnity machineXAxisInUnity = MachineAxisUnity.Y;
        [SerializeField] private float retractSign = -1f; //  w razie potrzeby odwrócić




        [SerializeField] private float _nominalDiameterMm = 0f;
        [SerializeField] private float _nominalLengthMm = 0f;

        [SerializeField] private float unitsPerMmX = 0.018f; // dla długości (X)
        [SerializeField] private float unitsPerMmY = 0.018f; // dla średnicy (Y)

        [SerializeField] private string _materialName = "";

        [SerializeField] private LatheStateController latheStateController;
        // standardowy półfabrykat (parametry)
        [SerializeField] private WorkpieceParams defaultWorkpieceParams;

        //[SerializeField] private LatheStateController latheStateController;
        [SerializeField] private WorkpieceController workpieceController;

        // wartości domyślne (lub pobierz je z WorkpieceController)
        [SerializeField] private MaterialType defaultMaterial = MaterialType.Stal;
        [SerializeField] private float defaultDiameterMm = 100f;
        [SerializeField] private float defaultLengthMm = 150f;


        [Header("Parting -> post flow")]
        [SerializeField] private float afterDropDelaySec = 1.2f;
        [SerializeField] private bool stopSpindleAfterGoHome = true;

        [Header("Wrong spindle direction (on cut contact)")]
        [SerializeField] private bool enableWrongDirectionCheck = true;
        [SerializeField] private float wrongDirRetractCooldown = 0.35f;
        [SerializeField] private bool stopSpindleOnWrongDirection = false;
        private bool _wrongDirWasTouching;

        private float _nextWrongDirRetractTime;


        [Header("SFX")]
        [SerializeField] private AudioSource sfxCut;
        [SerializeField] private float cutStopDelay = 0.12f;

        [SerializeField] private int partingMaxSolidPixelsNearCenter = 2; // dopuszczamy 0..2 piksele

        private float _cutStopAt;

        private Sprite _sourceSprite;
        private Color _sourceColor;

        [SerializeField] private byte materialAlphaThreshold = 10;
        [SerializeField] private byte emptyColumnAlphaThreshold = 2;

        

        [SerializeField] private MaterialColorConfig _materialColorConfig;

 


#if UNITY_WEBGL && !UNITY_EDITOR
[DllImport("__Internal")]
private static extern void DownloadFile(byte[] array, int length, string fileName, string mimeType);

private void WebGL_DownloadBytes(byte[] data, string fileName, string mimeType)
{
    DownloadFile(data, data.Length, fileName, mimeType);
}
#endif

        // public event System.Action DownloadPngRequested;

        public string MaterialName => _materialName;

        public void SetMaterialName(string materialName)
        {
            _materialName = materialName ?? "";
            Debug.Log($"[MATERIAL SET] {_materialName}");
        }



        private void RecalcUnitsFromNominal()
        {
            if (!workpieceRenderer || !workpieceRenderer.sprite) return;
            if (_nominalLengthMm <= 0.01f || _nominalDiameterMm <= 0.01f) return;

            Bounds b = workpieceRenderer.bounds; // już w WORLD, już po ApplyUnits()
            unitsPerMmX = b.size.x / _nominalLengthMm;
            unitsPerMmY = b.size.y / _nominalDiameterMm;

            //Debug.Log($"[UNITS CAL] unitsPerMmX={unitsPerMmX:F6} unitsPerMmY={unitsPerMmY:F6} " +
              //        $"(boundsW={b.size.x:F3}, boundsH={b.size.y:F3}, nomL={_nominalLengthMm}, nomD={_nominalDiameterMm})");
        }


        private void Awake()
        {

            drawingUI.CloseRequested += OnDrawingCloseRequested;
            drawingUI.DownloadPngRequested += OnDownloadPngRequested;

            if (!workpieceRenderer) workpieceRenderer = GetComponent<SpriteRenderer>();

            // ✅  zapisujemy oryginał PRZED wszelkimi podmianami
            _sourceSprite = workpieceRenderer.sprite;
            _sourceColor = workpieceRenderer.color;


        }

        private void OnDestroy()
        {
            if (drawingUI != null)
            {
                drawingUI.CloseRequested -= OnDrawingCloseRequested;
                drawingUI.DownloadPngRequested -= OnDownloadPngRequested; // ✅wypisanie
            }
        }

        
        private void OnDrawingCloseRequested()
        {
            drawingUI.Hide();

            // przywrócić standardowy półfabrykat
            if (workpieceController != null)
                workpieceController.ApplyFromMm(defaultMaterial, defaultDiameterMm, defaultLengthMm);

            // przebudować teksturę runtime
            TryInitRuntimeTexture();

            // przywrócić sterowanie
            if (toolPosition != null)
                toolPosition.SetInputEnabled(true);

            // na wszelki wypadek zresetować flagę, aby ponownie można było ciąć
            _isPartedOff = false;

            LatheSafetyLock.Unlock();
        }
       
        public void SetWorkpieceNominal(float diameterMm, float lengthMm)
        {
            Debug.Log($"[NOMINAL SET] D={_nominalDiameterMm} L={_nominalLengthMm}");
            _nominalDiameterMm = Mathf.Max(0f, diameterMm);
            _nominalLengthMm = Mathf.Max(0f, lengthMm);

            RecalcUnitsFromNominal();  // WAŻNE
        }



        private void Start()
        {
            TryInitRuntimeTexture();
        }

        public void TryInitRuntimeTexture()
        {
            if (!workpieceRenderer || !workpieceRenderer.sprite) return;

            // 0) Zawsze używamy oryginalnego (tego samego) sprite’a bazowego.
            // WAŻNE: NIE używać sprite’a runtime po poprzedniej inicjalizacji.
            // Skoro sprite jest zawsze ten sam — używamy sprite’a przypisanego do prefabu / początkowo.
            // Jeśli posiadasz _sourceSprite — użyj go. W przeciwnym razie używamy bieżącego,
            // ale poniżej zabezpieczamy się, aby nie pobierać _tex (runtime).
            Sprite spr = _sourceSprite != null ? _sourceSprite : workpieceRenderer.sprite;
            if (spr == null) return;

            // Jeśli aktualny sprite jest już runtime (tekstura = _tex), to i tak używamy _sourceSprite.
            if (_tex != null && spr.texture == _tex && _sourceSprite != null)
                spr = _sourceSprite;

            // 1) Określamy kolor materiału na podstawie nazwy (MaterialName)
            Color materialColor = ResolveMaterialColorFromName(_materialName);

            _spriteLocalBounds = spr.bounds;

            Rect rect = spr.rect;
            _texW = Mathf.RoundToInt(rect.width);
            _texH = Mathf.RoundToInt(rect.height);
            if (_texW <= 4 || _texH <= 4) return;

            _centerY = _texH / 2;

            Texture2D srcTex = spr.texture;

            Color32[] srcPixels;
            try { srcPixels = srcTex.GetPixels32(); }
            catch
            {
                Debug.LogWarning("[TryInitRuntimeTexture] Texture not readable. Enable Read/Write in Import Settings.");
                return;
            }

            int rx = Mathf.RoundToInt(rect.x);
            int ry = Mathf.RoundToInt(rect.y);

            // 2) Kopiujemy kształt (alfę) z oryginalnego sprite’a
            _pixels = new Color32[_texW * _texH];
            for (int y = 0; y < _texH; y++)
            {
                int srcRow = (ry + y) * srcTex.width;
                int dstRow = y * _texW;

                for (int x = 0; x < _texW; x++)
                    _pixels[dstRow + x] = srcPixels[srcRow + (rx + x)];
            }

            // 3) Normalizujemy alfę: każda „materia” staje się całkowicie nieprzezroczysta
            for (int i = 0; i < _pixels.Length; i++)
            {
                if (_pixels[i].a > 0)
                    _pixels[i].a = 255;
            }


            // 4) WYPALAMY kolor materiału bezpośrednio w piksele (BEZ kumulowania!)
            // Nie mnożymy przez poprzedni kolor, ponieważ _pixels zostały właśnie pobrane z oryginalnego sprite’a.
            ApplyMaterialColorToOpaquePixels(_pixels, (Color32)materialColor);

            // 5) Zapisujemy „nieprzetworzone” dane jako bazę z poprawnym kolorem materiału
            _basePixels = (Color32[])_pixels.Clone();

            // 6) Tworzymy / odtwarzamy teksturę runtime
            if (_tex == null || _tex.width != _texW || _tex.height != _texH)
            {
                _tex = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false);
                _tex.filterMode = FilterMode.Point;
                _tex.wrapMode = TextureWrapMode.Clamp;
            }

            // 7) Obrys / kontur (modyfikuje _pixels)
            PaintInitialOutlineOutside();

            _tex.SetPixels32(_pixels);
            _tex.Apply(false, false);

            float ppu = spr.pixelsPerUnit;

            Sprite newSprite = Sprite.Create(
                _tex,
                new Rect(0, 0, _texW, _texH),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect
            );

            workpieceRenderer.sprite = newSprite;

            // 8) Renderer nie jest już w ogóle tintowany — kolor znajduje się bezpośrednio w pikselach
            workpieceRenderer.color = Color.white;

            RecalcUnitsFromNominal();
        }

        private static void ApplyMaterialColorToOpaquePixels(Color32[] px, Color32 mat)
        {
            // Ważne: NIE wykonujemy MultiplyRgb na już pokolorowanym obrazie.
            // Ustawiamy kolor materiału bezpośrednio, zachowując alfę kształtu.
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                px[i] = new Color32(mat.r, mat.g, mat.b, px[i].a);
            }
        }

        private Color ResolveMaterialColorFromName(string materialName)
        {
            // Tutaj są dwie ścieżki:
            // 1) jeśli w Inspectorze istnieje MaterialColorConfig — użyj go
            // 2) w przeciwnym razie fallback na hardcoded wartości

            if (string.IsNullOrWhiteSpace(materialName))
                return Color.white;

            string m = materialName.Trim().ToLowerInvariant();

            // Jeśli istnieje konfiguracja:
            if (_materialColorConfig != null)
            {
                if (m.Contains("steel") || m.Contains("stal"))
                    return _materialColorConfig.stalColor;
                if (m.Contains("alu") || m.Contains("aluminium") || m.Contains("aluminum"))
                    return _materialColorConfig.aluminiumColor;
                if (m.Contains("brass") || m.Contains("mosiądz") || m.Contains("mosiadz"))
                    return _materialColorConfig.mosiądzColor;
            }

            // Fallback:
            if (m.Contains("steel") || m.Contains("stal")) return new Color(0.75f, 0.75f, 0.78f);
            if (m.Contains("alu") || m.Contains("aluminium") || m.Contains("aluminum")) return new Color(0.85f, 0.87f, 0.92f);
            if (m.Contains("brass") || m.Contains("mosiądz") || m.Contains("mosiadz")) return new Color(0.90f, 0.80f, 0.35f);

            return Color.white;
        }

       
        private void Update()
        {
            if (_isPartedOff) return;

            AutoFindToolTipAndColliders();

            if (Crash_ToolHitsChuckOrJaws()) return;
            if (Crash_ToolBodyHitsWorkpiece()) return;

            if (enableCrashChecks)
            {
                if (Crash_NoRotationTouch()) return;
                if (Crash_WrongDirectionTouch()) return;
                if (Crash_RapidTouchTooLong()) return;
            }

            //if (Crash_WrongDirectionTouch()) return;

            if (!CanCutNow()) return;
            if (!workpieceRenderer || _tex == null || _pixels == null) return;
            if (!toolTip || !_toolTipCollider) return;

            UpdateSpindlePhase();

            if (cutMode == CutMode.ProfileCarve)
            {
                if (_toolTipCollider is BoxCollider2D box)
                {
                    Vector2[] obbW = GetToolOBBWorld(box);
                    CarveByInsertOBB(obbW);
                }
                else
                {
                    CarveByInsertCollider(_toolTipCollider);
                }
            }
            else
            {
                if (_toolTipCollider is BoxCollider2D box)
                {
                    Vector2[] obbW = GetToolOBBWorld(box);
                    CarveLatheRevolveByInsertOBB(obbW);
                }
                else
                {
                    CarveLatheRevolveByInsertCollider(_toolTipCollider);
                }
            }
            if (sfxCut && sfxCut.isPlaying && Time.time > _cutStopAt)
                sfxCut.Stop();
        }

        // ================= SAFETY =================

        private void AutoFindToolTipAndColliders()
        {
            _refindTimer -= Time.deltaTime;
            if (_refindTimer > 0f) return;
            _refindTimer = refindInterval;

            if (toolTip == null)
            {
                var go = GameObject.FindGameObjectWithTag(toolTipTag);
                if (go != null)
                {
                    toolTip = go.transform;
                    _toolProfile = toolTip.GetComponent<ToolCutProfile>();
                }
            }

            if (toolTip != null && _toolTipCollider == null)
                _toolTipCollider = toolTip.GetComponent<Collider2D>();

            var bodyGOs = GameObject.FindGameObjectsWithTag(toolBodyTag);
            if (bodyGOs != null && bodyGOs.Length > 0)
            {
                var list = new System.Collections.Generic.List<Collider2D>();
                foreach (var go in bodyGOs)
                {
                    if (!go) continue;
                    var c = go.GetComponent<Collider2D>();
                    if (c) list.Add(c);
                }
                _toolBodyColliders = list.ToArray();
            }

            var hazardGOs = GameObject.FindGameObjectsWithTag(chuckHazardTag);
            if (hazardGOs != null && hazardGOs.Length > 0)
            {
                var list = new System.Collections.Generic.List<Collider2D>();
                foreach (var go in hazardGOs)
                {
                    if (!go) continue;
                    var c = go.GetComponent<Collider2D>();
                    if (c) list.Add(c);
                }
                _chuckHazards = list.ToArray();
            }
        }

        private bool Crash_NoRotationTouch()
        {
            if (!workpieceCollider) return false;
            if (_toolTipCollider == null && (_toolBodyColliders == null || _toolBodyColliders.Length == 0)) return false;

            bool spindleNotRotating = !spindle || !spindle.SpindleEnabled || spindle.CurrentRpm <= rpmZeroThreshold;
            if (!spindleNotRotating) return false;

            if (AnyToolTouchesWorkpiece())
            {
                TriggerCrash("[CRASH] Uderzenie narzędzia: wrzeciono nie obraca się");
                return true;
            }

            return false;
        }

        private bool Crash_RapidTouchTooLong()
        {
            if (!workpieceCollider || !_toolTipCollider) return false;
            if (!axisMotion) return false;

            bool isRapid = axisMotion.CurrentFeedMode == AxisMotionController.Mode.Rapid;
            if (!isRapid)
            {
                _rapidContactTimer = 0f;
                return false;
            }

            bool tipTouch = _toolTipCollider.IsTouching(workpieceCollider);

            // jeśli nie ma kontaktu — reset
            if (!tipTouch)
            {
                _rapidContactTimer = 0f;
                return false;
            }

            // jeśli kontakt istnieje, ale pod narzędziem nie ma materiału — nie jest to kontakt z detalem
            if (!HasMaterialUnderTool(_toolTipCollider))
            {
                _rapidContactTimer = 0f;
                return false;
            }

            // ✅ właśnie tutaj „można się dotknąć”: zliczamy czas kontaktu
            _rapidContactTimer += Time.deltaTime;

            if (_rapidContactTimer >= rapidContactLimitSec)
            {
                TriggerCrash("[CRASH] Zbyt duża prędkość: tryb Rapid powoduje wjazd w materiał");
                return true;
            }

            return false;
        }

        private bool AnyToolTouchesWorkpiece()
        {
            // 1) ToolTip
            bool tipTouch = false;
            if (_toolTipCollider && workpieceCollider && _toolTipCollider.IsTouching(workpieceCollider))
            {
                tipTouch = HasMaterialUnderTool(_toolTipCollider); // <-- главное
            }

            // 2) Tool body
            bool bodyTouch = false;
            if (_toolBodyColliders != null && workpieceCollider)
            {
                for (int i = 0; i < _toolBodyColliders.Length; i++)
                {
                    var c = _toolBodyColliders[i];
                    if (!c) continue;

                    if (c.IsTouching(workpieceCollider))
                    {
                        if (HasMaterialUnderTool(c)) { bodyTouch = true; break; } // <-- главное
                    }
                }
            }

            return tipTouch || bodyTouch;
        }

        private void TriggerCrash(string message)
        {
            if (Time.time < _nextCrashTime) return;
            _nextCrashTime = Time.time + crashCooldown;

            axisMotion?.StopMove();
            toolPosition?.StopMove();

            if (sfxCut)
            {
                sfxCut.Stop();
                _cutStopAt = 0f;
            }

            if (latheButtonsUI)
            {
                latheButtonsUI.PressStopExternal();
                latheButtonsUI.RefreshLampsExternal();
            }
            else if (spindle)
            {
                spindle.StopSpindle();
            }

            if (toolPosition) toolPosition.XMm -= retractMm;

            _rapidContactTimer = 0f;

            Debug.LogWarning(message);

            if (latheState != null)
            {
                latheState.EnterCrashState(message);
            }
            else if (CrashPopupUI.Instance != null)
            {
                CrashPopupUI.Instance.Show(message);
            }


        }

        private bool CanCutNow()
        {
            if (!spindle) return true;
            if (!spindle.IsClamped) return false;
            if (!spindle.SpindleEnabled) return false;
            if (spindle.CurrentRpm < minRpmToCut) return false;

            return true;
        }

        private void UpdateSpindlePhase()
        {
            if (!spindle) return;
            float rpm = spindle.CurrentRpm;
            float revPerSec = rpm / 60f;
            _spindlePhase01 = (_spindlePhase01 + revPerSec * Time.deltaTime) % 1f;
        }

        // ================= PIXEL / WORLD =================

        private int WorldXToPixel(float worldX, Bounds wpWorldBounds)
        {
            float nx = Mathf.InverseLerp(wpWorldBounds.min.x, wpWorldBounds.max.x, worldX);
            return Mathf.Clamp(Mathf.RoundToInt(nx * (_texW - 1)), 0, _texW - 1);
        }

        private int WorldYToPixel(float worldY, Bounds wpB)
        {
            float ny = Mathf.InverseLerp(wpB.min.y, wpB.max.y, worldY);
            return Mathf.Clamp(Mathf.RoundToInt(ny * (_texH - 1)), 0, _texH - 1);
        }

        private float PixelToWorldX(int px, Bounds wpWorldBounds)
        {
            float nx = (float)px / Mathf.Max(1, (_texW - 1));
            return Mathf.Lerp(wpWorldBounds.min.x, wpWorldBounds.max.x, nx);
        }

        private float PixelToWorldY(int py, Bounds wpB)
        {
            float ny = (float)py / Mathf.Max(1, (_texH - 1));
            return Mathf.Lerp(wpB.min.y, wpB.max.y, ny);
        }

        private Vector2[] GetToolOBBWorld(BoxCollider2D col)
        {
            Vector2 off = col.offset;
            Vector2 hs = col.size * 0.5f;

            Vector2[] local = new Vector2[4]
            {
                off + new Vector2(-hs.x, -hs.y),
                off + new Vector2(-hs.x,  hs.y),
                off + new Vector2( hs.x,  hs.y),
                off + new Vector2( hs.x, -hs.y),
            };

            Vector2[] world = new Vector2[4];
            Transform t = col.transform;

            for (int i = 0; i < 4; i++)
            {
                Vector3 w = t.TransformPoint(local[i]);
                world[i] = new Vector2(w.x, w.y);
            }

            return world;
        }

        // ================= ProfileCarve =================

        private void CarveByInsertOBB(Vector2[] obbWorld)
        {
            if (!workpieceRenderer || _tex == null || _pixels == null) return;

            Bounds wpB = workpieceRenderer.bounds;

            float minX = obbWorld[0].x, maxX = obbWorld[0].x;
            float minY = obbWorld[0].y, maxY = obbWorld[0].y;
            for (int i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, obbWorld[i].x);
                maxX = Mathf.Max(maxX, obbWorld[i].x);
                minY = Mathf.Min(minY, obbWorld[i].y);
                maxY = Mathf.Max(maxY, obbWorld[i].y);
            }

            minX = Mathf.Max(minX, wpB.min.x);
            maxX = Mathf.Min(maxX, wpB.max.x);
            minY = Mathf.Max(minY, wpB.min.y);
            maxY = Mathf.Min(maxY, wpB.max.y);
            if (minX >= maxX || minY >= maxY) return;

            int px0 = WorldXToPixel(minX, wpB);
            int px1 = WorldXToPixel(maxX, wpB);
            if (px0 > px1) { int t = px0; px0 = px1; px1 = t; }

            int py0 = WorldYToPixel(minY, wpB);
            int py1 = WorldYToPixel(maxY, wpB);
            if (py0 > py1) { int t = py0; py0 = py1; py1 = t; }

            // ✅ zabezpieczenie
            px0 = Mathf.Clamp(px0, 0, _texW - 1);
            px1 = Mathf.Clamp(px1, 0, _texW - 1);
            py0 = Mathf.Clamp(py0, 0, _texH - 1);
            py1 = Mathf.Clamp(py1, 0, _texH - 1);

            GetOBBFrame(obbWorld, out Vector2 center, out Vector2 axisX, out Vector2 axisY, out float halfX, out float halfY);

            bool changed = false;
            bool[] touchedX = paintWholeColumnOnTouch ? new bool[_texW] : null;

            bool[] allowCutX = null;
            if (enableHelix && axisMotion != null && spindle != null)
            {
                float feedMmPerSec = axisMotion.GetCurrentSpeedMmPerSec();
                float rpm = spindle.CurrentRpm;

                if (feedMmPerSec > 0.0001f && rpm > 0.0001f && unitsPerMm > 1e-6f)
                {
                    float feedMmPerMin = feedMmPerSec * 60f;
                    float pitchMm = Mathf.Max(minPitchMmPerRev, feedMmPerMin / rpm);

                    allowCutX = new bool[_texW];
                    for (int x = px0; x <= px1; x++)
                    {
                        float wx = PixelToWorldX(x, wpB);
                        float xMm = (wx - wpB.min.x) / unitsPerMm;
                        float helixPhase = Mathf.Repeat((xMm / pitchMm) + _spindlePhase01, 1f);
                        allowCutX[x] = (helixPhase <= helixDuty);
                    }
                }
            }

            for (int y = py0; y <= py1; y++)
            {
                float wy = PixelToWorldY(y, wpB);
                for (int x = px0; x <= px1; x++)
                {
                    if (allowCutX != null && !allowCutX[x]) continue;

                    float wx = PixelToWorldX(x, wpB);
                    Vector2 p = new Vector2(wx, wy);

                    if (!PointInOBB(p, center, axisX, axisY, halfX, halfY))
                        continue;

                    int idx = y * _texW + x;
                    if (_pixels[idx].a == 0) continue;

                    _pixels[idx].a = 0;
                    changed = true;
                    if (touchedX != null) touchedX[x] = true;

                    if (mirrorCutByRotation)
                    {
                        int mirrorY = (_centerY * 2) - y;
                        if (mirrorY >= 0 && mirrorY < _texH)
                        {
                            int idx2 = mirrorY * _texW + x;
                            if (_pixels[idx2].a != 0)
                            {
                                _pixels[idx2].a = 0;
                                changed = true;
                            }
                            if (touchedX != null) touchedX[x] = true;
                        }
                    }
                }
            }

            if (changed && paintWholeColumnOnTouch && touchedX != null)
            {
                Color32 c = (Color32)machinedColumnColor;
                //Color32 c = (Color32)GetMachinedColor();
                for (int x = px0; x <= px1; x++)
                {
                    if (!touchedX[x]) continue;
                    PaintWholeColumn(x, c);
                }
            }

            if (changed)
            {
                NotifyCutSfx();
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);

                if (enablePartingDetect)
                {
                    int check0 = Mathf.Max(0, px0 - 4);
                    int check1 = Mathf.Min(_texW - 1, px1 + 4);
                    TryDetectAndHandleParting(check0, check1);
                }
            }
        }

        private void CarveByInsertCollider(Collider2D col2D)
        {
            if (!workpieceRenderer || _tex == null || _pixels == null) return;

            Bounds wpB = workpieceRenderer.bounds;
            Bounds cB = col2D.bounds;

            float minX = Mathf.Max(cB.min.x, wpB.min.x);
            float maxX = Mathf.Min(cB.max.x, wpB.max.x);
            float minY = Mathf.Max(cB.min.y, wpB.min.y);
            float maxY = Mathf.Min(cB.max.y, wpB.max.y);
            if (minX >= maxX || minY >= maxY) return;

            int px0 = WorldXToPixel(minX, wpB);
            int px1 = WorldXToPixel(maxX, wpB);
            if (px0 > px1) { int t = px0; px0 = px1; px1 = t; }

            int py0 = WorldYToPixel(minY, wpB);
            int py1 = WorldYToPixel(maxY, wpB);
            if (py0 > py1) { int t = py0; py0 = py1; py1 = t; }

            // ✅ zabezpieczenie
            px0 = Mathf.Clamp(px0, 0, _texW - 1);
            px1 = Mathf.Clamp(px1, 0, _texW - 1);
            py0 = Mathf.Clamp(py0, 0, _texH - 1);
            py1 = Mathf.Clamp(py1, 0, _texH - 1);

            bool changed = false;
            bool[] touchedX = paintWholeColumnOnTouch ? new bool[_texW] : null;

            for (int y = py0; y <= py1; y++)
            {
                float wy = PixelToWorldY(y, wpB);
                for (int x = px0; x <= px1; x++)
                {
                    float wx = PixelToWorldX(x, wpB);
                    Vector2 p = new Vector2(wx, wy);

                    if (!col2D.OverlapPoint(p))
                        continue;

                    int idx = y * _texW + x;
                    if (_pixels[idx].a == 0) continue;

                    _pixels[idx].a = 0;
                    changed = true;
                    if (touchedX != null) touchedX[x] = true;

                    if (mirrorCutByRotation)
                    {
                        int mirrorY = (_centerY * 2) - y;
                        if (mirrorY >= 0 && mirrorY < _texH)
                        {
                            int idx2 = mirrorY * _texW + x;
                            if (_pixels[idx2].a != 0)
                            {
                                _pixels[idx2].a = 0;
                                changed = true;
                            }
                            if (touchedX != null) touchedX[x] = true;
                        }
                    }
                }
            }

            if (changed && paintWholeColumnOnTouch && touchedX != null)
            {
                

                Color32 c = (Color32)machinedColumnColor;
                //Color32 c = (Color32)GetMachinedColor();
                for (int x = px0; x <= px1; x++)
                {
                    if (!touchedX[x]) continue;
                    PaintWholeColumn(x, c);
                }
            }

            if (changed)
            {
                NotifyCutSfx();
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);

                if (enablePartingDetect)
                {
                    int check0 = Mathf.Max(0, px0 - 4);
                    int check1 = Mathf.Min(_texW - 1, px1 + 4);
                    TryDetectAndHandleParting(check0, check1);
                }
            }
        }

        private void PaintWholeColumn(int x, Color32 c)
        {
            x = Mathf.Clamp(x, 0, _texW - 1);

            for (int y = 0; y < _texH; y++)
            {
                int idx = y * _texW + x;
                if (_pixels[idx].a == 0) continue;
                _pixels[idx] = c;
            }
        }

        private void GetOBBFrame(Vector2[] obb, out Vector2 center, out Vector2 axisX, out Vector2 axisY, out float halfX, out float halfY)
        {
            center = (obb[0] + obb[2]) * 0.5f;
            Vector2 e0 = obb[1] - obb[0];
            Vector2 e1 = obb[3] - obb[0];

            halfY = e0.magnitude * 0.5f;
            halfX = e1.magnitude * 0.5f;

            axisY = (halfY > 1e-6f) ? (e0 / (2f * halfY)) : Vector2.up;
            axisX = (halfX > 1e-6f) ? (e1 / (2f * halfX)) : Vector2.right;
        }

        private bool PointInOBB(Vector2 p, Vector2 c, Vector2 axisX, Vector2 axisY, float halfX, float halfY)
        {
            Vector2 d = p - c;
            float lx = Vector2.Dot(d, axisX);
            float ly = Vector2.Dot(d, axisY);
            return Mathf.Abs(lx) <= halfX && Mathf.Abs(ly) <= halfY;
        }

        // ================= LatheRevolve =================

        private void CarveLatheRevolveByInsertOBB(Vector2[] obbWorld)
        {
            if (!workpieceRenderer || _tex == null || _pixels == null) return;

            Bounds wpB = workpieceRenderer.bounds;

            float minX = obbWorld[0].x, maxX = obbWorld[0].x;
            for (int i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, obbWorld[i].x);
                maxX = Mathf.Max(maxX, obbWorld[i].x);
            }

            minX = Mathf.Max(minX, wpB.min.x);
            maxX = Mathf.Min(maxX, wpB.max.x);
            if (minX >= maxX) return;

            int x0 = WorldXToPixel(minX, wpB);
            int x1 = WorldXToPixel(maxX, wpB);
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }

            x0 = Mathf.Clamp(x0, 0, _texW - 1);
            x1 = Mathf.Clamp(x1, 0, _texW - 1);

            GetOBBFrame(obbWorld, out Vector2 center, out Vector2 axisX, out Vector2 axisY, out float halfX, out float halfY);

            float centerYWorld = wpB.center.y;
            float halfRadWorld = wpB.extents.y;

            bool changed = false;

            float rpm = Mathf.Max(0.0001f, spindle ? spindle.CurrentRpm : 0f);
            float feedMmPerSec = axisMotion ? axisMotion.GetCurrentSpeedMmPerSec() : 0f;
            float feedMmPerMin = feedMmPerSec * 60f;
            float pitchMm = Mathf.Max(minPitchMmPerRev, feedMmPerMin / rpm);

            for (int x = x0; x <= x1; x++)
            {
                float wx = PixelToWorldX(x, wpB);

                if (enableHelix && feedMmPerSec > 0.0001f)
                {
                    float xMm = (wx - wpB.min.x) / Mathf.Max(1e-6f, unitsPerMm);
                    float helixPhase = Mathf.Repeat((xMm / pitchMm) + _spindlePhase01, 1f);
                    if (helixPhase > helixDuty)
                        continue;
                }

                float minAbsR = float.PositiveInfinity;

                // szukamy minimalnego promienia przecięcia OBB dla tego X
                for (int y = 0; y < _texH; y++)
                {
                    float wy = PixelToWorldY(y, wpB);
                    Vector2 p = new Vector2(wx, wy);

                    if (!PointInOBB(p, center, axisX, axisY, halfX, halfY))
                        continue;

                    float absR = Mathf.Abs(wy - centerYWorld);
                    if (absR < minAbsR) minAbsR = absR;
                }

                if (float.IsPositiveInfinity(minAbsR))
                    continue;

                int targetRadiusPx = Mathf.Clamp(
                    Mathf.RoundToInt((minAbsR / halfRadWorld) * (_texH / 2f)),
                    0, (_texH / 2) - 1
                );

                int top = _centerY + targetRadiusPx;
                int bot = _centerY - targetRadiusPx;

                // przycinamy od góry
                for (int y = top + 1; y < _texH; y++)
                {
                    int idx = y * _texW + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                // przycinamy od dołu
                for (int y = 0; y < bot; y++)
                {
                    int idx = y * _texW + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                PaintMachinedBand(x, top, -1);
                PaintMachinedBand(x, bot, +1);
            }

            if (changed)
            {
                NotifyCutSfx();
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);

                if (enablePartingDetect)
                {
                    int check0 = Mathf.Max(0, x0 - 4);
                    int check1 = Mathf.Min(_texW - 1, x1 + 4);
                    TryDetectAndHandleParting(check0, check1);
                }
            }
        }

        private void CarveLatheRevolveByInsertCollider(Collider2D col2D)
        {
            if (!workpieceRenderer || _tex == null || _pixels == null) return;

            Bounds wpB = workpieceRenderer.bounds;
            Bounds cb = col2D.bounds;

            float minX = Mathf.Max(cb.min.x, wpB.min.x);
            float maxX = Mathf.Min(cb.max.x, wpB.max.x);
            if (minX >= maxX) return;

            int x0 = WorldXToPixel(minX, wpB);
            int x1 = WorldXToPixel(maxX, wpB);
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }

            x0 = Mathf.Clamp(x0, 0, _texW - 1);
            x1 = Mathf.Clamp(x1, 0, _texW - 1);

            float centerYWorld = wpB.center.y;
            float halfRadWorld = wpB.extents.y;

            bool changed = false;

            for (int x = x0; x <= x1; x++)
            {
                float wx = PixelToWorldX(x, wpB);

                float minAbsR = float.PositiveInfinity;

                for (int y = 0; y < _texH; y++)
                {
                    float wy = PixelToWorldY(y, wpB);
                    if (!col2D.OverlapPoint(new Vector2(wx, wy)))
                        continue;

                    float absR = Mathf.Abs(wy - centerYWorld);
                    if (absR < minAbsR) minAbsR = absR;
                }

                if (float.IsPositiveInfinity(minAbsR))
                    continue;

                int targetRadiusPx = Mathf.Clamp(
                    Mathf.RoundToInt((minAbsR / halfRadWorld) * (_texH / 2f)),
                    0, (_texH / 2) - 1
                );

                int top = _centerY + targetRadiusPx;
                int bot = _centerY - targetRadiusPx;

                for (int y = top + 1; y < _texH; y++)
                {
                    int idx = y * _texW + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                for (int y = 0; y < bot; y++)
                {
                    int idx = y * _texW + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                PaintMachinedBand(x, top, -1);
                PaintMachinedBand(x, bot, +1);
            }

            if (changed)
            {
                NotifyCutSfx();
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);

                if (enablePartingDetect)
                {
                    int check0 = Mathf.Max(0, x0 - 4);
                    int check1 = Mathf.Min(_texW - 1, x1 + 4);
                    TryDetectAndHandleParting(check0, check1);
                }
            }
        }

        private void PaintMachinedBand(int x, int yEdge, int dirToInside)
        {
            x = Mathf.Clamp(x, 0, _texW - 1);

            for (int t = 0; t < edgeThicknessPx; t++)
            {
                int y = yEdge + dirToInside * t;
                if (y < 0 || y >= _texH) break;

                int idx = y * _texW + x;
                if (_pixels[idx].a == 0) continue;

                _pixels[idx] = (Color32)machinedEdgeColor;
            }

            if (!softenEdge) return;

            int y2 = yEdge + dirToInside;
            if (y2 < 0 || y2 >= _texH) return;

            int idx2 = y2 * _texW + x;
            if (_pixels[idx2].a == 0) return;

            var c = _pixels[idx2];
            c.a = (byte)Mathf.Min(255, c.a + softenAlphaAdd);
            _pixels[idx2] = c;
        }

        // ================= Chuck crash checks =================

        private bool Crash_ToolHitsChuckOrJaws()
        {
            if (!enableChuckChecks) return false;
            if (_chuckHazards == null || _chuckHazards.Length == 0) return false;

            if (_toolTipCollider != null)
            {
                for (int i = 0; i < _chuckHazards.Length; i++)
                {
                    var hz = _chuckHazards[i];
                    if (!hz) continue;

                    if (_toolTipCollider.IsTouching(hz))
                    {
                        TriggerCrash("[CRASH] Zderzenie płytki skrawającej z uchwytem/szczękami");
                        return true;
                    }
                }
            }

            if (_toolBodyColliders != null)
            {
                for (int b = 0; b < _toolBodyColliders.Length; b++)
                {
                    var body = _toolBodyColliders[b];
                    if (!body) continue;

                    for (int i = 0; i < _chuckHazards.Length; i++)
                    {
                        var hz = _chuckHazards[i];
                        if (!hz) continue;

                        if (body.IsTouching(hz))
                        {
                            TriggerCrash("[CRASH] Zderzenie oprawki/rezcedrżaka z uchwytem/szczękami");
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool Crash_ToolBodyHitsWorkpiece()
        {
            if (!workpieceCollider) return false;
            if (_toolBodyColliders == null || _toolBodyColliders.Length == 0) return false;

            for (int i = 0; i < _toolBodyColliders.Length; i++)
            {
                var body = _toolBodyColliders[i];
                if (!body) continue;

                if (body.IsTouching(workpieceCollider))
                {
                    if (!HasMaterialUnderTool(body))
                        continue;

                    TriggerCrash("[CRASH] Uderzenie narzędzia w detal (oprawka/rezcedrżak)");
                    return true;
                }
            }

            return false;
        }

        // ================= Parting =================

        private bool IsColumnEmptyNearCenter(int x)
        {
            x = Mathf.Clamp(x, 0, _texW - 1);

            int span = Mathf.RoundToInt(_texH * 0.40f);
            int yMin = Mathf.Max(0, _centerY - span);
            int yMax = Mathf.Min(_texH - 1, _centerY + span);

            int maxA = 0;
            for (int y = yMin; y <= yMax; y++)
            {
                int a = _pixels[y * _texW + x].a;
                if (a > maxA) maxA = a;
                if (a > emptyColumnAlphaThreshold)
                    return false;
            }

            Debug.Log($"[EMPTYCOL] x={x} maxA={maxA} thr={emptyColumnAlphaThreshold}");
            return true;
        }

        private bool HasAnyMaterialInColumn(int x)
        {
            x = Mathf.Clamp(x, 0, _texW - 1);

            for (int y = 0; y < _texH; y++)
            {
                if (_pixels[y * _texW + x].a > emptyColumnAlphaThreshold)
                    return true;
            }
            return false;
        }
        private bool HasAnyMaterialLeftOf(int cutX)
        {
            cutX = Mathf.Clamp(cutX, 0, _texW - 1);
            for (int x = 0; x < cutX; x++)
                if (!IsColumnEmptyNearCenter(x)) return true;
            return false;
        }

        private bool HasAnyMaterialRightOf(int cutX)
        {
            cutX = Mathf.Clamp(cutX, 0, _texW - 1);
            for (int x = cutX + 1; x < _texW; x++)
                if (!IsColumnEmptyNearCenter(x)) return true;
            return false;
        }
        private void HandlePartedOff(int cutX)
        {
            _isPartedOff = true;

            GameObject cutPartGO = CreateCutPartFromRightSide(cutX);

            ClearRightSideOnWorkpiece(cutX);
            _tex.SetPixels32(_pixels);
            _tex.Apply(false, false);

            //ApplyMaterialColorTo(workpieceRenderer);

            if (!cutPartGO) return;

            var sr = cutPartGO.GetComponent<SpriteRenderer>();
            if (!sr || !sr.sprite)
            {
                Debug.LogWarning("[PartedOff] Cut part has no SpriteRenderer or Sprite");
                return;
            }
            var spr = sr.sprite;
            var tex = spr.texture;

            Debug.Log($"[DRAW SRC] sprRect={spr.rect} tex={tex.width}x{tex.height} " +
                      $"sr.color={sr.color} wpColor={workpieceRenderer.color}");

            // ✅ sprawdzenie: czy w ogóle istnieją nieprzezroczyste piksele w tex
            var px = tex.GetPixels32();
            int solid = 0;
            for (int i = 0; i < px.Length; i += 10) // krok 10 — aby nie spowalniać
            {
                if (px[i].a > 10) { solid++; if (solid > 20) break; }
            }
            Debug.Log($"[DRAW SRC] solidSamples={solid} (alpha>10)");

            Vector3 stableLossyScale = sr.transform.lossyScale;

            // Ważne: obliczamy współczynniki od razu (przed spadkiem), tak jak było wcześniej
            // RecalcUnitsFromNominal();
            // float uY = workpieceRenderer.bounds.size.y / _nominalDiameterMm;
            // float uX = workpieceRenderer.bounds.size.x / _nominalLengthMm;
            // drawingUI.SetUnits(uX, uY);
            RecalcUnitsFromNominal();
            drawingUI.SetUnits(unitsPerMmX, unitsPerMmY);
            drawingUI.SetMaterialColor(sr.color);

            drawingUI.SetWorkpieceNominal(
    _nominalDiameterMm,
    _nominalLengthMm
);
            if (sfxCut)
            {
                sfxCut.Stop();
                _cutStopAt = 0f;
            }


            if (partDropAnimator != null)
            {
                partDropAnimator.Play(cutPartGO.transform, () =>
                {

                    drawingUI.SetMaterialName(MaterialName);
                    drawingUI.ShowSingleViewWithDims(sr, stableLossyScale);
                    // po zakończeniu animacji — uruchamiamy logikę końcową
                    StartCoroutine(PartedOff_PostFlow(sr, stableLossyScale));
                });
            }
            else
            {

                drawingUI.SetMaterialName(MaterialName);
                drawingUI.ShowSingleViewWithDims(sr, stableLossyScale);
                // jeśli nie ma animacji — również uruchamiamy logikę końcową
                StartCoroutine(PartedOff_PostFlow(sr, stableLossyScale));
            }
        }

        private GameObject CreateCutPartFromRightSide(int cutX)
        {
            cutX = Mathf.Clamp(cutX, 0, _texW - 1);

            // 1) pomijamy „nacięcie” w prawo: idziemy od cutX do pierwszej NIEpustej kolumny
            int srcX0 = cutX + 1;   // ✅ zaczynamy na prawo od płaszczyzny odcięcia
            while (srcX0 < _texW && !HasAnyMaterialInColumn(srcX0))
                srcX0++;

            // jeśli po prawej stronie w ogóle nie ma materiału — nie ma czego ciąć
            if (srcX0 >= _texW) return null;

            // 2) szerokość od srcX0 do ostatniej kolumny włącznie
            int partW = _texW - srcX0; // WAŻNE: BEZ +1

            Debug.Log($"[PART] cutX={cutX} srcX0={srcX0} partW={partW} texW={_texW}");

            if (partW <= 2) return null;

            // --- texture ---
            var partTex = new Texture2D(partW, _texH, TextureFormat.RGBA32, false);
            partTex.filterMode = FilterMode.Point;
            partTex.wrapMode = TextureWrapMode.Clamp;

            var partPixels = new Color32[partW * _texH];
            for (int y = 0; y < _texH; y++)
            {
                int dstRow = y * partW;
                int srcRow = y * _texW;
                for (int x = 0; x < partW; x++)
                {
                    partPixels[dstRow + x] = _pixels[srcRow + (srcX0 + x)];
                }
            }

            partTex.SetPixels32(partPixels);
            partTex.Apply(false, false);

            // --- sprite ---
            float ppu = workpieceRenderer.sprite.pixelsPerUnit;
            var partSprite = Sprite.Create(
                partTex,
                new Rect(0, 0, partW, _texH),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect
            );

            // --- object ---
            var go = new GameObject("CutPart_Right");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = partSprite;
            sr.sortingLayerID = workpieceRenderer.sortingLayerID;
            sr.sortingOrder = workpieceRenderer.sortingOrder + 1;

            // ✅ aby odcięta część była z tego samego materiału
            // sr.color = workpieceRenderer.material.color; // либо _materialColor
            // ✅ ustawiamy ten sam kolor materiału
            //sr.color = workpieceRenderer != null ? workpieceRenderer.color : Color.white;
            // sr.color = Color.white;
            sr.color = workpieceRenderer != null ? workpieceRenderer.color : Color.white;

            Bounds wpB = workpieceRenderer.bounds;
            float worldX0 = PixelToWorldX(srcX0, wpB);
            float worldX1 = PixelToWorldX(_texW - 1, wpB);
            float centerX = (worldX0 + worldX1) * 0.5f;

            Transform parent = cutPartSpawnRoot ? cutPartSpawnRoot : workpieceRenderer.transform.parent;
            go.transform.SetParent(parent, false);

            go.transform.position = new Vector3(centerX, wpB.center.y, workpieceRenderer.transform.position.z);
            go.transform.rotation = workpieceRenderer.transform.rotation;

            SetWorldScale(go.transform, workpieceRenderer.transform.lossyScale);

            Debug.Log($"WP lossy={workpieceRenderer.transform.lossyScale} CUT lossy={go.transform.lossyScale}");
            return go;
        }


        private void ClearRightSideOnWorkpiece(int cutX)
        {
            cutX = Mathf.Clamp(cutX, 0, _texW - 1);

            int srcX0 = cutX + 1;

            for (int y = 0; y < _texH; y++)
            {
                int row = y * _texW;
                for (int x = srcX0; x < _texW; x++)
                {
                    int idx = row + x;
                    _pixels[idx].a = 0;
                }
            }
        }

        
        private void TryDetectAndHandleParting(int x0, int x1)
        {
            if (_isPartedOff) return;
            if (Time.time < _nextPartingCheckTime) return;
            _nextPartingCheckTime = Time.time + partingCheckCooldown;

            x0 = Mathf.Clamp(x0, 1, _texW - 2);
            x1 = Mathf.Clamp(x1, 1, _texW - 2);
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }

            GetMaterialSpan(out int firstMat, out int lastMat);
            if (firstMat < 0 || lastMat < 0) return;

            int from = Mathf.Max(x0, firstMat + 1);
            int to = Mathf.Min(x1, lastMat - 1);
            if (from > to) return;

            // ✅ (opcjonalnie, ale przydatne): aby nie uruchamiało się, gdy po prawej/lewej stronie nie ma już detalu
            int mid = (from + to) / 2;
            if (!HasAnyMaterialLeftOf(mid) || !HasAnyMaterialRightOf(mid))
                return;

            for (int cx = from; cx <= to; cx++)
            {
                if (!IsColumnCutNearCenter(cx))
                    continue;

                // ✅ płaszczyznę odcięcia wyznaczamy na podstawie noża
                int cutX = GetCutXFromToolPosition();

               // Debug.Log($"[PART DETECT] foundGapX={cx} cutX(used)={cx}");
                HandlePartedOff(cx);
                return;
            }
        }

       
        private void GetMaterialSpan(out int firstX, out int lastX)
        {
            firstX = -1;
            lastX = -1;

            for (int x = 0; x < _texW; x++)
            {
                if (!IsColumnEmpty(x)) { firstX = x; break; }
            }
            for (int x = _texW - 1; x >= 0; x--)
            {
                if (!IsColumnEmpty(x)) { lastX = x; break; }
            }
        }

        private bool IsColumnEmpty(int x)
        {
            x = Mathf.Clamp(x, 0, _texW - 1);

            for (int y = 0; y < _texH; y++)
            {
                int idx = y * _texW + x;
                if (_pixels[idx].a > emptyColumnAlphaThreshold)
                    return false;
            }
            return true;
        }

        // ================= Utils =================

        private static void ComputeAlphaBounds(
            Color32[] pixels, int w, int h, byte alphaThreshold,
            out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = w; minY = h;
            maxX = -1; maxY = -1;

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[row + x].a <= alphaThreshold)
                        continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
            {
                minX = minY = 0;
                maxX = maxY = 0;
            }
        }

        private bool HasMaterialUnderTool(Collider2D toolCol)
        {
            if (!refineCrashByPixels) return true; // jeśli wyłączone — zakładamy, że materiał istnieje
            if (_tex == null || _pixels == null || !workpieceRenderer || toolCol == null) return false;

            Bounds wpB = workpieceRenderer.bounds;
            Bounds tb = toolCol.bounds;

            // szybka filtracja
            if (!tb.Intersects(wpB)) return false;

            // przecięcie w przestrzeni świata
            float minX = Mathf.Max(tb.min.x, wpB.min.x);
            float maxX = Mathf.Min(tb.max.x, wpB.max.x);
            float minY = Mathf.Max(tb.min.y, wpB.min.y);
            float maxY = Mathf.Min(tb.max.y, wpB.max.y);
            if (minX >= maxX || minY >= maxY) return false;

            // konwersja do pikseli
            int px0 = Mathf.Clamp(WorldXToPixel(minX, wpB), 0, _texW - 1);
            int px1 = Mathf.Clamp(WorldXToPixel(maxX, wpB), 0, _texW - 1);
            int py0 = Mathf.Clamp(WorldYToPixel(minY, wpB), 0, _texH - 1);
            int py1 = Mathf.Clamp(WorldYToPixel(maxY, wpB), 0, _texH - 1);
            if (px0 > px1) { int t = px0; px0 = px1; px1 = t; }
            if (py0 > py1) { int t = py0; py0 = py1; py1 = t; }

            int step = Mathf.Clamp(crashPixelStep, 1, 8);

            for (int y = py0; y <= py1; y += step)
            {
                float wy = PixelToWorldY(y, wpB);
                int row = y * _texW;

                for (int x = px0; x <= px1; x += step)
                {
                    // czy w tym pikselu w ogóle istnieje materiał?
                    if (_pixels[row + x].a <= crashAlphaThreshold) continue;

                    float wx = PixelToWorldX(x, wpB);
                    Vector2 p = new Vector2(wx, wy);

                    // punkt musi rzeczywiście znajdować się wewnątrz kolidera narzędzia
                    if (toolCol.OverlapPoint(p))
                        return true;
                }
            }

            return false;
        }

        private static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            Transform p = t.parent;
            if (p == null)
            {
                t.localScale = worldScale;
                return;
            }

            Vector3 pLossy = p.lossyScale;
            t.localScale = new Vector3(
                pLossy.x != 0 ? worldScale.x / pLossy.x : worldScale.x,
                pLossy.y != 0 ? worldScale.y / pLossy.y : worldScale.y,
                pLossy.z != 0 ? worldScale.z / pLossy.z : worldScale.z
            );
        }

        
        private IEnumerator PartedOff_PostFlow(SpriteRenderer sr, Vector3 stableLossyScale)
        {
            // 0) od razu blokujemy sterowanie, aby gracz nie przeszkadzał
            if (toolPosition != null)
                toolPosition.SetInputEnabled(false);

            // 1) pauza po opadnięciu (jeśli potrzebna)
            if (afterDropDelaySec > 0f)
                yield return new WaitForSeconds(afterDropDelaySec);

            // 3) STOP (po dojechaniu do home)
            if (latheStateController != null)
                latheStateController.PressStop();

            LatheSafetyLock.Lock();

            // 2) jedziemy do pozycji home i CZEKAmy, aż dojedzie
            if (axisMotion != null)
            {
                axisMotion.GoHome();

                while (axisMotion.IsHoming)
                    yield return null;
            }

        }


        private void OnDownloadPngRequested()
        {
            if (drawingUI == null) return;

            Texture2D tex = drawingUI.GetSheetTextureCopy();
            if (tex == null)
            {
                Debug.LogWarning("[PNG] No captured UI texture to save.");
                return;
            }

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);

            string fileName = $"Drawing_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";


#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var extensions = new[] { new ExtensionFilter("PNG Image", "png") };

            string path = StandaloneFileBrowser.SaveFilePanel(
                "Save drawing",
                "",                 // katalog startowy (można ustawić Application.persistentDataPath)
                fileName,
                extensions
            );

            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("[PNG] Save cancelled.");
                return;
            }

            // na wszelki wypadek: jeśli użytkownik nie podał .png
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                path += ".png";

            File.WriteAllBytes(path, png);
            Debug.Log($"[PNG] Saved: {path}");

            // otworzyć folder i zaznaczyć plik (Windows)
            Application.OpenURL("file:///" + Path.GetDirectoryName(path));
#else
    // fallback как было (для других платформ)
    //SaveToPersistentFolder(png, defaultName);
    PngSaver.SaveToPersistentFolder(png, fileName);
#endif
        }


        private bool Crash_WrongDirectionTouch()
        {
            if (!workpieceCollider) return false;
            if (_toolTipCollider == null && (_toolBodyColliders == null || _toolBodyColliders.Length == 0)) return false;

            // wrzeciono musi być włączone i obracać się
            if (!spindle || !spindle.SpindleEnabled || spindle.CurrentRpm <= rpmZeroThreshold)
                return false;

            // ✅ interesuje nas wyłącznie REVERSE = CW
            if (spindle.CurrentSpinDirection != SpinDirection.CW)
                return false;

            // kontakt bezpośrednio z materiałem (a nie z pustą przestrzenią)
            if (AnyToolTouchesWorkpiece())
            {
                TriggerCrash("[ERROR] Niewłaściwy kierunek obrotów (CCW / Reverse). Toczenie możliwe tylko na CW / Forward.");
                return true;
            }

            return false;
        }

        private void NotifyCutSfx()
        {
            if (!sfxCut) return;

            // ✅ jeśli wrzeciono już się nie obraca — nie włączamy
            if (!spindle || !spindle.SpindleEnabled || spindle.CurrentRpm <= rpmZeroThreshold)
                return;

            if (!sfxCut.isPlaying)
                sfxCut.Play();

            _cutStopAt = Time.time + cutStopDelay;
        }

        private bool IsColumnCutNearCenter(int x)
        {
            x = Mathf.Clamp(x, 0, _texW - 1);

            int span = Mathf.RoundToInt(_texH * 0.40f);
            int yMin = Mathf.Max(0, _centerY - span);
            int yMax = Mathf.Min(_texH - 1, _centerY + span);

            int solid = 0;
            for (int y = yMin; y <= yMax; y++)
            {
                if (_pixels[y * _texW + x].a > emptyColumnAlphaThreshold)
                {
                    solid++;
                    if (solid > partingMaxSolidPixelsNearCenter)
                        return false; // nadal istnieje mostek
                }
            }
            return true; // prawie pusto => uznajemy za odcięte
        }

        private int GetCutXFromToolPosition()
        {
            Bounds wpB = workpieceRenderer.bounds;
            float toolX = _toolTipCollider.bounds.center.x;   // środek noża
            return Mathf.Clamp(WorldXToPixel(toolX, wpB), 1, _texW - 2);
        }

       
        private void PaintInitialOutlineOutside()
        {
            if (_pixels == null || _texW <= 0 || _texH <= 0) return;

            Color32 edge = (Color32)machinedEdgeColor;

            for (int x = 0; x < _texW; x++)
            {
                int top = -1;
                int bot = -1;

                // top: od góry w dół szukamy pierwszego „materiału”
                for (int y = _texH - 1; y >= 0; y--)
                {
                    if (_pixels[y * _texW + x].a > emptyColumnAlphaThreshold)
                    {
                        top = y;
                        break;
                    }
                }

                // bot: od dołu w górę
                for (int y = 0; y < _texH; y++)
                {
                    if (_pixels[y * _texW + x].a > emptyColumnAlphaThreshold)
                    {
                        bot = y;
                        break;
                    }
                }

                if (top < 0 || bot < 0) continue;

                // ✅ rysujemy NA ZEWNĄTRZ:
                // z góry — powyżej top
                // z dołu — poniżej bot
                for (int t = 1; t <= edgeThicknessPx; t++)
                {
                    int y = top + t;
                    if (y >= _texH) break;

                    int idx = y * _texW + x;

                    // ✅ było: == 0
                    if (_pixels[idx].a <= emptyColumnAlphaThreshold)
                        _pixels[idx] = edge;
                }

                // z dołu — poniżej bot
                for (int t = 1; t <= edgeThicknessPx; t++)
                {
                    int y = bot - t;
                    if (y < 0) break;

                    int idx = y * _texW + x;

                    // ✅ było: == 0
                    if (_pixels[idx].a <= emptyColumnAlphaThreshold)
                        _pixels[idx] = edge;
                }

                PaintBandOnlyOnEmpty(x, top + 1, +1, edgeThicknessPx, edge); // w górę
                PaintBandOnlyOnEmpty(x, bot - 1, -1, edgeThicknessPx, edge); // w dół
            }
        }

        private void PaintBandOnlyOnEmpty(int x, int startY, int dir, int thickness, Color32 color)
        {
            x = Mathf.Clamp(x, 0, _texW - 1);

            for (int t = 0; t < thickness; t++)
            {
                int y = startY + dir * t;
                if (y < 0 || y >= _texH) break;

                int idx = y * _texW + x;

                // ✅ WAŻNE: rysujemy WYŁĄCZNIE po pustej przestrzeni (poza materiałem)
                if (_pixels[idx].a > emptyColumnAlphaThreshold)
                    break; // dotarliśmy do materiału — dalej nie kontynuujemy

                // można rysować kolorem „szarym”, ustawiając alfę jako widoczną
                var c = color;
                c.a = 255;
                _pixels[idx] = c;
            }
        }

        /*private LatheTrainer.Machine.MaterialType GetMaterialTypeFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return LatheTrainer.Machine.MaterialType.Stal; // domyślnie

            string n = name.Trim().ToLowerInvariant();

            // najczęstsze warianty
            if (n.Contains("steel") || n.Contains("stal"))
                return LatheTrainer.Machine.MaterialType.Stal;

            if (n.Contains("alu") || n.Contains("alum") || n.Contains("aluminium") || n.Contains("aluminum"))
                return LatheTrainer.Machine.MaterialType.Aluminium;

            if (n.Contains("brass") || n.Contains("mos") || n.Contains("mądz") || n.Contains("ms"))
                return LatheTrainer.Machine.MaterialType.Mosiądz;

            // jeśli pojawi się coś nieznanego
            return LatheTrainer.Machine.MaterialType.Stal;
        }*/

        private MaterialType ParseMaterialType(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName))
                return MaterialType.Stal;

            materialName = materialName.Trim().ToLowerInvariant();

            if (materialName.Contains("stal") || materialName.Contains("stal"))
                return MaterialType.Stal;

            if (materialName.Contains("alu") || materialName.Contains("aluminium") || materialName.Contains("aluminum"))
                return MaterialType.Aluminium;

            if (materialName.Contains("brass") || materialName.Contains("mos") || materialName.Contains("mosiądz") || materialName.Contains("mosiadz"))
                return MaterialType.Mosiądz;

            return MaterialType.Stal;
        }

        private static Color MakeMachinedColor(Color baseColor,
    float brighten = 0.12f,      // + jasność
    float desaturate = 0.10f,    // - nasycenie
    float contrast = 0.08f)      // + kontrast
        {
            // 1) korekty HSV (jasność / nasycenie)
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);

            s = Mathf.Clamp01(s * (1f - desaturate));
            v = Mathf.Clamp01(v * (1f + brighten));

            Color c = Color.HSVToRGB(h, s, v);

            // 2) kontrast wokół 0.5 (nieznacznie)
            c.r = Mathf.Clamp01((c.r - 0.5f) * (1f + contrast) + 0.5f);
            c.g = Mathf.Clamp01((c.g - 0.5f) * (1f + contrast) + 0.5f);
            c.b = Mathf.Clamp01((c.b - 0.5f) * (1f + contrast) + 0.5f);

            c.a = 1f;
            return c;
        }

        private Color GetBaseMaterialColor()
        {
            // 1) określamy typ na podstawie ciągu znaków
            var type = ParseMaterialType(MaterialName);

            // 2) pobieramy kolor z ScriptableObject (jeśli jest zdefiniowany)
            if (_materialColorConfig != null)
                return _materialColorConfig.GetColor(type);

            // fallback — biały
            return Color.white;
        }

        private Color GetMachinedColor()
        {
            var baseColor = GetBaseMaterialColor();
            return MakeMachinedColor(baseColor, brighten: 0.12f, desaturate: 0.10f, contrast: 0.08f);
        }
    }
}
