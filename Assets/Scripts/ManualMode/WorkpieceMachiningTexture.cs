using LatheTrainer.Core;
using UnityEngine;
using LatheTrainer.UI;

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
        private Collider2D _toolBodyCollider;  // kolider oprawki/uchwytu (dowolny Collider2D w obiektach nadrzędnych ToolTip)
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
        [SerializeField] private float rpmZeroThreshold = 0.1f;      // uznać za „nie obraca się”
        [SerializeField] private float rapidContactLimitSec = 0.5f;   // kontakt w trybie Rapid > 0,5 s
        [SerializeField] private float retractMm = 50f;               // odsunięcie w osi X
        [SerializeField] private float crashCooldown = 0.35f;         // zabezpieczenie przed spamem
        private float _rapidContactTimer;
        private float _nextCrashTime;

        // ====== Runtime texture ======
        private Texture2D _tex;
        private Color32[] _pixels;
        private Color32[] _basePixels;
        private int _w, _h;
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

        // znalezione kolidery
        private Collider2D[] _chuckHazards;
        private Collider2D[] _toolBodyColliders; // WSZYSTKIE ToolBody na scenie/prefabie

        [SerializeField] private LatheStateController latheState;

        private void Awake()
        {
            if (!workpieceRenderer) workpieceRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            TryInitRuntimeTexture();
        }

        public void TryInitRuntimeTexture()
        {
            if (!workpieceRenderer || !workpieceRenderer.sprite) return;

            _spriteLocalBounds = workpieceRenderer.sprite.bounds;

            var rect = workpieceRenderer.sprite.rect;
            _w = (int)rect.width;
            _h = (int)rect.height;
            if (_w <= 4 || _h <= 4) return;

            _centerY = _h / 2;

            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Clamp;

            _pixels = new Color32[_w * _h];

            Color32 baseColor = (Color32)workpieceRenderer.color;
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = baseColor;

            _basePixels = (Color32[])_pixels.Clone();

            _tex.SetPixels32(_pixels);
            _tex.Apply(false, false);

            var newSprite = Sprite.Create(
                _tex,
                new Rect(0, 0, _w, _h),
                new Vector2(0.5f, 0.5f),
                workpieceRenderer.sprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect
            );

            workpieceRenderer.sprite = newSprite;
            workpieceRenderer.color = Color.white;
        }

        private void Update()
        {
            AutoFindToolTipAndColliders();

            // ZAWSZE — niezależnie od obrotów, trybu itp.
            if (Crash_ToolHitsChuckOrJaws())
                return;

            // 1) Zawsze: ToolBody nie może dotykać detalu
            if (Crash_ToolBodyHitsWorkpiece())
                return;

            // ====== KONTROLE BEZPIECZEŃSTWA (PRZED skrawaniem!) ======
            if (enableCrashChecks)
            {
                if (Crash_NoRotationTouch()) return;
                if (Crash_RapidTouchTooLong()) return;
            }

            // ====== dalej — normalne skrawanie ======
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
            else // LatheRevolve
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
        }

        // ================= SAFETY =================

        private void AutoFindToolTipAndColliders()
        {
            _refindTimer -= Time.deltaTime;
            if (_refindTimer > 0f) return;
            _refindTimer = refindInterval;

            // ToolTip
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

            // ToolBody (może być jeden lub kilka)
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

            // ChuckHazard (uchwyt + szczęki)
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
            if (!workpieceCollider || (!_toolTipCollider && !_toolBodyCollider)) return false;

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
            bool tipTouch = _toolTipCollider.IsTouching(workpieceCollider);

            if (isRapid && tipTouch)
            {
                _rapidContactTimer += Time.deltaTime;

                if (_rapidContactTimer >= rapidContactLimitSec)
                {
                    TriggerCrash("[CRASH] Zbyt duża prędkość: tryb Rapid powoduje wjazd w materiał");
                    return true;
                }
            }
            else
            {
                _rapidContactTimer = 0f;
            }

            return false;
        }

        private bool AnyToolTouchesWorkpiece()
        {
            bool tipTouch = _toolTipCollider && _toolTipCollider.IsTouching(workpieceCollider);
            bool bodyTouch = _toolBodyCollider && _toolBodyCollider.IsTouching(workpieceCollider);
            return tipTouch || bodyTouch;
        }

        private void TriggerCrash(string message)
        {
            if (Time.time < _nextCrashTime) return;
            _nextCrashTime = Time.time + crashCooldown;

            // 1) zatrzymanie ruchu
            axisMotion?.StopMove();
            toolPosition?.StopMove(); // если нет такого метода — убери

            // 2) zatrzymanie wrzeciona przez UI (aby zaktualizować kontrolki)
            // 2) zatrzymanie wrzeciona „jak przyciskiem STOP” + aktualizacja kontrolek
            if (latheButtonsUI)
            {
                latheButtonsUI.PressStopExternal();   // wywołuje OnStop -> StopSpindle + ApplyLamps
                latheButtonsUI.RefreshLampsExternal(); // na wszelki wypadek, jeśli stan został zmieniony gdzie indziej

            }
            else if (spindle)
            {
                spindle.StopSpindle();
            }

            // 3) odsunięcie narzędzia w osi X w dół o -50
            if (toolPosition) toolPosition.XMm -= retractMm;

            // 4) reset licznika kontaktu w trybie Rapid
            _rapidContactTimer = 0f;

            Debug.LogWarning(message);

            // wyświetlić okno modalne
            if (latheState != null)
            {
                latheState.EnterCrashState(message);
            }
            else if (CrashPopupUI.Instance != null)
            {
                CrashPopupUI.Instance.Show(message);
            }
        }

        // ================= CUT CONDITIONS =================

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

        // ================= SKRAWANIE PIKSELOWE (twoje funkcje) =================

        private int WorldXToPixel(float worldX, Bounds wpWorldBounds)
        {
            float nx = Mathf.InverseLerp(wpWorldBounds.min.x, wpWorldBounds.max.x, worldX);
            return Mathf.Clamp(Mathf.RoundToInt(nx * (_w - 1)), 0, _w - 1);
        }

        private int WorldYToPixel(float worldY, Bounds wpB)
        {
            float ny = Mathf.InverseLerp(wpB.min.y, wpB.max.y, worldY);
            return Mathf.Clamp(Mathf.RoundToInt(ny * (_h - 1)), 0, _h - 1);
        }

        private float PixelToWorldX(int px, Bounds wpWorldBounds)
        {
            float nx = (float)px / Mathf.Max(1, (_w - 1));
            return Mathf.Lerp(wpWorldBounds.min.x, wpWorldBounds.max.x, nx);
        }

        private float PixelToWorldY(int py, Bounds wpB)
        {
            float ny = (float)py / Mathf.Max(1, (_h - 1));
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

        // ===== ProfileCarve by OBB =====
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

            GetOBBFrame(obbWorld, out Vector2 center, out Vector2 axisX, out Vector2 axisY, out float halfX, out float halfY);

            bool changed = false;
            bool[] touchedX = paintWholeColumnOnTouch ? new bool[_w] : null;

            // helix mask per X
            bool[] allowCutX = null;
            if (enableHelix && axisMotion != null && spindle != null)
            {
                float feedMmPerSec = axisMotion.GetCurrentSpeedMmPerSec();
                float rpm = spindle.CurrentRpm;

                if (feedMmPerSec > 0.0001f && rpm > 0.0001f && unitsPerMm > 1e-6f)
                {
                    float feedMmPerMin = feedMmPerSec * 60f;
                    float pitchMm = Mathf.Max(minPitchMmPerRev, feedMmPerMin / rpm);

                    allowCutX = new bool[_w];
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

                    int idx = y * _w + x;
                    if (_pixels[idx].a == 0) continue;

                    _pixels[idx].a = 0;
                    changed = true;
                    if (touchedX != null) touchedX[x] = true;

                    if (mirrorCutByRotation)
                    {
                        int mirrorY = (_centerY * 2) - y;
                        if (mirrorY >= 0 && mirrorY < _h)
                        {
                            int idx2 = mirrorY * _w + x;
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
                for (int x = px0; x <= px1; x++)
                {
                    if (!touchedX[x]) continue;
                    PaintWholeColumn(x, c);
                }
            }

            if (changed)
            {
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);
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

        // ===== ProfileCarve by Collider2D (Polygon/Circle etc.) =====
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

            bool changed = false;
            bool[] touchedX = paintWholeColumnOnTouch ? new bool[_w] : null;

            for (int y = py0; y <= py1; y++)
            {
                float wy = PixelToWorldY(y, wpB);
                for (int x = px0; x <= px1; x++)
                {
                    float wx = PixelToWorldX(x, wpB);
                    Vector2 p = new Vector2(wx, wy);

                    if (!col2D.OverlapPoint(p))
                        continue;

                    int idx = y * _w + x;
                    if (_pixels[idx].a == 0) continue;

                    _pixels[idx].a = 0;
                    changed = true;
                    if (touchedX != null) touchedX[x] = true;

                    if (mirrorCutByRotation)
                    {
                        int mirrorY = (_centerY * 2) - y;
                        if (mirrorY >= 0 && mirrorY < _h)
                        {
                            int idx2 = mirrorY * _w + x;
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
                for (int x = px0; x <= px1; x++)
                {
                    if (!touchedX[x]) continue;
                    PaintWholeColumn(x, c);
                }
            }

            if (changed)
            {
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);
            }
        }

        private void PaintWholeColumn(int x, Color32 c)
        {
            for (int y = 0; y < _h; y++)
            {
                int idx = y * _w + x;
                if (_pixels[idx].a == 0) continue;
                _pixels[idx] = c;
            }
        }

        // ===== LatheRevolve OBB =====
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

                for (int y = 0; y < _h; y++)
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
                    Mathf.RoundToInt((minAbsR / halfRadWorld) * (_h / 2f)),
                    0, (_h / 2) - 1
                );

                int top = _centerY + targetRadiusPx;
                int bot = _centerY - targetRadiusPx;

                for (int y = top + 1; y < _h; y++)
                {
                    int idx = y * _w + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                for (int y = 0; y < bot; y++)
                {
                    int idx = y * _w + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                PaintMachinedBand(x, top, -1);
                PaintMachinedBand(x, bot, +1);
            }

            if (changed)
            {
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);
            }
        }

        // ===== LatheRevolve Collider2D =====
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

            float centerYWorld = wpB.center.y;
            float halfRadWorld = wpB.extents.y;

            bool changed = false;

            for (int x = x0; x <= x1; x++)
            {
                float wx = PixelToWorldX(x, wpB);

                float minAbsR = float.PositiveInfinity;

                for (int y = 0; y < _h; y++)
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
                    Mathf.RoundToInt((minAbsR / halfRadWorld) * (_h / 2f)),
                    0, (_h / 2) - 1
                );

                int top = _centerY + targetRadiusPx;
                int bot = _centerY - targetRadiusPx;

                for (int y = top + 1; y < _h; y++)
                {
                    int idx = y * _w + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                for (int y = 0; y < bot; y++)
                {
                    int idx = y * _w + x;
                    if (_pixels[idx].a == 0) continue;
                    _pixels[idx].a = 0;
                    changed = true;
                }

                PaintMachinedBand(x, top, -1);
                PaintMachinedBand(x, bot, +1);
            }

            if (changed)
            {
                _tex.SetPixels32(_pixels);
                _tex.Apply(false, false);
            }
        }

        private void PaintMachinedBand(int x, int yEdge, int dirToInside)
        {
            for (int t = 0; t < edgeThicknessPx; t++)
            {
                int y = yEdge + dirToInside * t;
                if (y < 0 || y >= _h) break;

                int idx = y * _w + x;
                if (_pixels[idx].a == 0) continue;

                _pixels[idx] = (Color32)machinedEdgeColor;
            }

            if (!softenEdge) return;

            int y2 = yEdge + dirToInside;
            if (y2 < 0 || y2 >= _h) return;
            int idx2 = y2 * _w + x;
            if (_pixels[idx2].a == 0) return;

            var c = _pixels[idx2];
            c.a = (byte)Mathf.Min(255, c.a + softenAlphaAdd);
            _pixels[idx2] = c;
        }

        private bool Crash_ToolHitsChuckOrJaws()
        {
            if (!enableChuckChecks) return false;
            if (_chuckHazards == null || _chuckHazards.Length == 0) return false;

            // 1) ToolTip vs hazards
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

            // 2) ToolBody vs hazards
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
                    TriggerCrash("[CRASH] Uderzenie narzędzia w detal (oprawka/rezcedrżak)");
                    return true;
                }
            }

            return false;
        }
    }
}