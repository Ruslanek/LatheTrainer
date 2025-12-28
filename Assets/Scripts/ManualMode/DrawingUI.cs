using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;




namespace LatheTrainer.UI
{
    public class DrawingUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject drawingPanel;
        [SerializeField] private RawImage sheetImage;
        [SerializeField] private TMP_Text infoText;

        [Header("Sheet (px)")]
        [SerializeField] private int sheetWidth = 1024;
        [SerializeField] private int sheetHeight = 576;

        [Header("Colors")]
        [SerializeField] private Color32 paper = new Color32(255, 255, 255, 255);
        [SerializeField] private Color32 ink = new Color32(20, 20, 20, 255);
        [SerializeField] private Color32 axis = new Color32(140, 140, 140, 255);

        [Header("Background removal (Color Key)")]
        [Tooltip("Jeśli tło nie jest przezroczyste (niebieskie), wycinamy według koloru tła. Jest on wykrywany automatycznie na podstawie narożników — to klucz zapasowy.")]
        [SerializeField] private Color32 fallbackBgKey = new Color32(16, 55, 125, 255);
        [Range(0, 100)]
        [SerializeField] private int bgTolerance = 18;

        [Header("Alpha / Material detect")]
        [Tooltip("Jeśli alpha <= threshold — uznajemy za tło (dla tekstur z przezroczystością).")]
        [Range(0, 255)]
        [SerializeField] private byte alphaThreshold = 10;

        [Header("Units")]
        [Tooltip("worldUnits na mm (np. 0.018)")]
        [SerializeField] private float unitsPerMm = 0.018f;

        [Header("Layout (mm -> sheet px)")]
        [SerializeField] private float sheetWidthMm = 297f;   // A4 landscape
        [SerializeField] private float sheetHeightMm = 210f;
        [SerializeField] private float marginLeftMm = 20f;
        [SerializeField] private float marginOtherMm = 5f;
        [SerializeField] private int blockGapMm = 5;
        [SerializeField] private int innerPadPx = 14;

        [Header("Line thickness (px)")]
        [SerializeField] private int profileOutlinePx = 3;
        [SerializeField] private int circleOutlinePx = 2;
        [SerializeField] private int axisLinePx = 1;

        [Header("Trim tail (left)")]
        [SerializeField] private bool trimLeftTail = true; //false;
        [SerializeField] private float tailRiseRatio = 0.18f;
        [SerializeField] private int tailConfirmRun = 6;


        [Header("Dimensions (px)")]
        [SerializeField] private int dimLinePx = 1;          // grubość linii wymiarowej
        [SerializeField] private int dimExtPx = 10;          // wysokość linii pomocniczych (w dół)
        [SerializeField] private int dimGapPx = 8;           // odstęp od konturu do linii wymiarowej
        [SerializeField] private int dimArrowSizePx = 6;     // rozmiar strzałek
        [SerializeField] private int dimTextOffsetPx = 10;   // odstęp tekstu od linii

        // ✅ ile miejsca zarezerwować na dole na wymiary / tekst (dodatkowy zapas)
        [Header("Dimensions layout reserve")]
        [SerializeField] private int dimReserveExtraPx = 10;

        [SerializeField] private Color32 dimTextColor = new Color32(0, 0, 0, 255);

        [Header("Layout reserves (px)")]
        [SerializeField] private int topViewDimReservePx = 80;   // miejsce na linie wymiarowe w widokach górnych

        [Header("Alignment")]
        [SerializeField] private int leftAlignPadPx = 32; // ogólny odstęp dla profilu i warstwy kolorowej

        [Header("Stamp text (visible values)")]
        [SerializeField] private string headerTitle = "Tokarka";
        [SerializeField] private int stampFontSize = 22;
        [SerializeField] private int stampLineSpacingPx = 18;


        [Header("Stamp TMP (fixed)")]
        [SerializeField] private TMP_Text stampTitle;
        [SerializeField] private TMP_Text stampMaterial;
        [SerializeField] private TMP_Text stampDate;

        [Header("Buttons")]
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button downloadPngButton;

        public event Action CloseRequested;
        public event Action DownloadPngRequested;


        [Header("Download capture")]
        [SerializeField] private RectTransform captureRect; // wskaż tutaj główny (korzeniowy) RectTransform panelu rysunku (lub arkusza)

        [SerializeField] private Canvas drawingCanvas;     // Canvas DrawingPanel
        [SerializeField] private Camera uiCamera;          // Kamera, która renderuje ten Canvas
        [SerializeField] private RectTransform sheetRect;  // sheetImage.rectTransform

        [SerializeField] private GameObject buttonsRoot;

        private Texture2D _sheetTex_Download; // tutaj umieszczamy GOTOWY zrzut panelu

        private Coroutine _captureRoutine;


        private string _materialName = "";

        private Color32 _materialTint = new Color32(255, 255, 255, 255);
        public void SetMaterialColor(Color c) => _materialTint = (Color32)c;

        private void UpdateStampText(string material, float lengthMm, float diamMm)
        {
            if (stampTitle) stampTitle.text = "Tokarka";

            if (stampMaterial)
                stampMaterial.text = $"Materiał: {material}\n  Ø={diamMm:0.0}mm L={lengthMm:0.0}mm";


            if (stampDate)
                stampDate.text = System.DateTime.Now.ToString("dd.MM.yyyy  HH:mm");
        }

      

        public void SetMaterialName(string materialName)
        {

            _materialName = materialName ?? "";
        }

        // --- runtime ---
        private Texture2D _sheetTex;
        private Color32[] _sheetPixels;
        private float _pxPerMmX;
        private float _pxPerMmY;

        private float _unitsPerMmX = 0.018f;
        private float _unitsPerMmY = 0.018f;

        private int MmToPxX(float mm) => Mathf.RoundToInt(mm * _pxPerMmX);
        private int MmToPxY(float mm) => Mathf.RoundToInt(mm * _pxPerMmY);

        // --- Nominal workpiece (mm) for calibration ---
        private float _nominalDiamMm;
        private float _nominalLenMm;

       
        [SerializeField] private int visibleInnerCirclePx = 2;

        // -------------------- Public API --------------------

        public void SetUnits(float unitsPerMmX, float unitsPerMmY)
        {
            _unitsPerMmX = Mathf.Max(1e-6f, unitsPerMmX);
            _unitsPerMmY = Mathf.Max(1e-6f, unitsPerMmY);

            

            Debug.Log($"[SETUNITS RECV] uX={_unitsPerMmX:F6} uY={_unitsPerMmY:F6}");
        }

        public void SetWorkpieceNominal(float diamMm, float lenMm)
        {
            _nominalDiamMm = Mathf.Max(0, diamMm);
            _nominalLenMm = Mathf.Max(0, lenMm);
        }

        public void Hide()
        {
            if (drawingPanel) drawingPanel.SetActive(false);
        }



        /// <summary>
        /// Główne wejście: przekaż SpriteRenderer od odciętego detalu (ten sam, który spada).
        /// Wewnątrz: usuwamy tło, przycinamy „ogon”, budujemy 3 widoki i układamy je na arkuszu
        /// </summary>
        public void ShowFromCutPart(SpriteRenderer cutPartSR)
        {
            if (!cutPartSR || !cutPartSR.sprite)
            {
                Debug.LogWarning("[DrawingUI] ShowFromCutPart: SpriteRenderer/Sprite missing.");
                return;
            }

            EnsureSheet();
            ClearSheet(paper);
           // ClearLabels();

            _pxPerMmX = sheetWidth / Mathf.Max(1f, sheetWidthMm);
            _pxPerMmY = sheetHeight / Mathf.Max(1f, sheetHeightMm);

            DrawGostFrame();
            RectInt inner = GetInnerFrameRect();

            Sprite sprite = cutPartSR.sprite;

            // 1) Profil (tylko materiał, bez tła)
            if (!TryExtractProfile(sprite, out int[] radiusPxPerX, out int lengthPx, out int cropLeftPx, out Color32 bgKeyUsed))
            {
                if (infoText) infoText.text = "Nie można odczytać profilu (pusta/nieczytelna tekstura).";
                ApplySheet();
                if (drawingPanel) drawingPanel.SetActive(true);
                return;
            }

            int rMax = GetMaxRadius(radiusPxPerX);
            if (rMax <= 0 || lengthPx <= 2)
            {
                if (infoText) infoText.text = "Profil пустой.";
                ApplySheet();
                if (drawingPanel) drawingPanel.SetActive(true);
                return;
            }

            // 2) Konwersja PIKSELE -> WORLD przez PPU i lossyScale (WAŻNE: osobno X i Y)
            float ppu = Mathf.Max(1e-6f, sprite.pixelsPerUnit);
            float scaleX = Mathf.Abs(cutPartSR.transform.lossyScale.x);
            float scaleY = Mathf.Abs(cutPartSR.transform.lossyScale.y);

            float diamWorldFromProfile = (2f * rMax) / ppu * scaleY; // Ø w world
            float lenWorldFromProfile = (lengthPx) / ppu * scaleX;   // L w world

            // 4) Końcowe wymiary w mm (WAŻNE: osobno jednostki X/Y)
            float diamMm = diamWorldFromProfile / Mathf.Max(1e-6f, _unitsPerMmY);
            float lengthMm = lenWorldFromProfile / Mathf.Max(1e-6f, _unitsPerMmX);

            float worldAspect = lengthMm / Mathf.Max(1e-6f, diamMm);

            if (infoText)
            {
                infoText.text =
                    $"OK\n" +
                    $"L={lengthMm:0.0}mm\n" +
                    $"Ø={diamMm:0.0}mm\n" +
                    $"lenPx={lengthPx}\n" +
                    $"rMaxPx={rMax}\n" +
                    $"ppu={ppu:0.###}\n" +
                    $"bgKey={bgKeyUsed.r},{bgKeyUsed.g},{bgKeyUsed.b}\n" +
                    $"MaterialName={_materialName}";
            }

            Debug.Log($"[DRAW] diamWorld={diamWorldFromProfile:F3} lenWorld={lenWorldFromProfile:F3} " +
                      $"ppu={ppu:F3} scale=({scaleX:F3},{scaleY:F3}) => Ø={diamMm:F1} L={lengthMm:F1}");

            // 5) Rozmieszczenie
            LayoutGost(inner, out RectInt profileRect, out RectInt frontRect, out RectInt colorRect, out RectInt stampRect);



            // ✅ 6) Skala „mm -> px” wspólna, ale profil z dodatkowym zapasem na dole pod wymiary
            int sharedCenterY = profileRect.yMin + profileRect.height / 2;

            int dimReservePx = GetProfileDimReservePx();
            float pxPerMm = ComputePxPerMmForProfileAndFront(profileRect, frontRect, lengthMm, diamMm, 16, 18, dimReservePx);

            // 7) Konwersja profilu: px -> mm
            float mmPerPxX = lengthMm / Mathf.Max(1, (lengthPx - 1));
            float mmPerPxY = diamMm / Mathf.Max(1, (2 * rMax));

            // int sharedCenterY = profileRect.yMin + profileRect.height / 2;

            DrawProfileViewMm(profileRect, radiusPxPerX, lengthPx, mmPerPxX, mmPerPxY, pxPerMm, dimReservePx, sharedCenterY);
            // DrawFrontViewMm(frontRect, diamMm, pxPerMm, sharedCenterY);
            DrawFrontViewLevelsMm(frontRect, radiusPxPerX, mmPerPxY, pxPerMm, sharedCenterY);


            // DrawProfileViewMm(profileRect, radiusPxPerX, lengthPx, mmPerPxX, mmPerPxY, pxPerMm, sharedCenterY);
            // DrawFrontViewMm(frontRect, diamMm, pxPerMm);

            // 8) Widok kolorowy: w rzeczywistych proporcjach świata
            //RectInt dstColor = ShrinkRect(colorRect, innerPadPx, innerPadPx);
            //BlitSpriteToRect_ColorKey(sprite, dstColor, cropLeftPx, bgKeyUsed, keepAspect: true, forcedAspect: worldAspect);
            //BlitSpriteToRect_ColorKey(sprite, dstColor, cropLeftPx, bgKeyUsed, keepAspect: true, forcedAspect: worldAspect, alignLeft: true);

            RectInt dstColor = ShrinkRect(colorRect, innerPadPx, innerPadPx);

            int commonLeftX = profileRect.xMin + leftAlignPadPx;
            //commonLeftX = Mathf.Clamp(commonLeftX, dstColor.xMin, dstColor.xMax - tw);// nie wychodzić poza blok

            BlitSpriteToRect_ColorKey(
                sprite, dstColor, cropLeftPx, bgKeyUsed,
                keepAspect: true,
                forcedAspect: worldAspect,
                forcedOx: commonLeftX
            );

            // 9) Ramki / opisy / stempel
            UpdateStampText(_materialName, _nominalLenMm, _nominalDiamMm);

            ApplySheet();
            if (drawingPanel) drawingPanel.SetActive(true);
        }

        // -------------------- Sheet --------------------

        private void EnsureSheet()
        {
            if (_sheetTex != null && _sheetTex.width == sheetWidth && _sheetTex.height == sheetHeight)
            {
                if (sheetImage && sheetImage.texture != _sheetTex) sheetImage.texture = _sheetTex;
                if (_sheetPixels == null || _sheetPixels.Length != sheetWidth * sheetHeight)
                    _sheetPixels = new Color32[sheetWidth * sheetHeight];
                return;
            }

            _sheetTex = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);
            _sheetTex.filterMode = FilterMode.Point;
            _sheetTex.wrapMode = TextureWrapMode.Clamp;

            _sheetPixels = new Color32[sheetWidth * sheetHeight];
            ClearSheet(paper);

            _sheetTex.SetPixels32(_sheetPixels);
            _sheetTex.Apply(false, false);

            if (sheetImage) sheetImage.texture = _sheetTex;
            EnsureLabelPool();
        }

        private void ClearSheet(Color32 c)
        {
            if (_sheetPixels == null || _sheetPixels.Length != sheetWidth * sheetHeight)
                _sheetPixels = new Color32[sheetWidth * sheetHeight];

            for (int i = 0; i < _sheetPixels.Length; i++)
                _sheetPixels[i] = c;
        }

        private void ApplySheet()
        {
            _sheetTex.SetPixels32(_sheetPixels);
            _sheetTex.Apply(false, false);
        }

        // -------------------- Extract profile + trim + bgKey --------------------

        private bool TryExtractProfile(
            Sprite sprite,
            out int[] radiusPxPerX,
            out int lengthPx,
            out int trimStartPx,
            out Color32 bgKeyUsed)
        {
            radiusPxPerX = null;
            lengthPx = 0;
            trimStartPx = 0;
            bgKeyUsed = fallbackBgKey;

            Texture2D tex = sprite.texture;
            if (!tex) return false;

            Rect sr = sprite.rect;
            int w = (int)sr.width;
            int h = (int)sr.height;
            if (w < 4 || h < 4) return false;

            int sx0 = (int)sr.x;
            int sy0 = (int)sr.y;

            Color32[] pixels;
            try { pixels = tex.GetPixels32(); }
            catch
            {
                Debug.LogWarning("[DrawingUI] Texture not readable. Enable Read/Write on texture.");
                return false;
            }

            bgKeyUsed = GuessBackgroundKey(pixels, tex.width, sx0, sy0, w, h);

            int centerY = h / 2;
            int[] tmp = new int[w];

            bool any = false;
            int minMatX = w - 1;
            int maxMatX = 0;

            for (int x = 0; x < w; x++)
            {
                int topMost = -1;
                int botMost = -1;

                for (int y = h - 1; y >= 0; y--)
                {
                    Color32 c = pixels[(sy0 + y) * tex.width + (sx0 + x)];
                    if (IsMaterial(c, bgKeyUsed)) { topMost = y; break; }
                }

                for (int y = 0; y < h; y++)
                {
                    Color32 c = pixels[(sy0 + y) * tex.width + (sx0 + x)];
                    if (IsMaterial(c, bgKeyUsed)) { botMost = y; break; }
                }

                if (topMost < 0 || botMost < 0)
                {
                    tmp[x] = 0;
                    continue;
                }



                any = true;
                minMatX = Mathf.Min(minMatX, x);
                maxMatX = Mathf.Max(maxMatX, x);

                int radTop = Mathf.Abs(topMost - centerY);
                int radBot = Mathf.Abs(botMost - centerY);
                tmp[x] = Mathf.Max(radTop, radBot);
            }

            if (!any) return false;

            lengthPx = (maxMatX - minMatX) + 1;
            if (lengthPx <= 2) return false;

            radiusPxPerX = new int[lengthPx];
            for (int i = 0; i < lengthPx; i++)
                radiusPxPerX[i] = tmp[minMatX + i];

            // trim tail
            int trimLocal = 0;
            if (trimLeftTail)
            {
                int rMax = GetMaxRadius(radiusPxPerX);
                //trimLocal = FindMainStartIndex(radiusPxPerX, rMax);

                trimLocal = FindMainStartIndexSafe(radiusPxPerX, rMax);
                //trimLocal = FindMainStartIndex(radiusPxPerX, rMax);

                if (trimLocal > 0 && lengthPx - trimLocal > 8)
                {
                    int newLen = lengthPx - trimLocal;
                    var trimmed = new int[newLen];
                    Array.Copy(radiusPxPerX, trimLocal, trimmed, 0, newLen);
                    radiusPxPerX = trimmed;
                    lengthPx = newLen;
                }
                else trimLocal = 0;
            }

            // cropLeftPx dla color view = minMatX + trimLocal
            trimStartPx = minMatX + trimLocal;
            return true;
        }

        private bool IsMaterial(Color32 c, Color32 bgKey)
        {
            return c.a > alphaThreshold;
        }

        private static bool ColorClose(Color32 a, Color32 b, int tol)
        {
            int dr = a.r - b.r;
            int dg = a.g - b.g;
            int db = a.b - b.b;
            return (dr * dr + dg * dg + db * db) <= (tol * tol);
        }

        private Color32 GuessBackgroundKey(Color32[] px, int texW, int sx0, int sy0, int w, int h)
        {
            Color32 c1 = px[(sy0 + 0) * texW + (sx0 + 0)];
            Color32 c2 = px[(sy0 + 0) * texW + (sx0 + (w - 1))];
            Color32 c3 = px[(sy0 + (h - 1)) * texW + (sx0 + 0)];
            Color32 c4 = px[(sy0 + (h - 1)) * texW + (sx0 + (w - 1))];

            Color32[] cs = { c1, c2, c3, c4 };
            int best = 0;
            int bestScore = int.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                int score = 0;
                for (int k = 0; k < 4; k++)
                {
                    if (i == k) continue;
                    int dr = cs[i].r - cs[k].r;
                    int dg = cs[i].g - cs[k].g;
                    int db = cs[i].b - cs[k].b;
                    score += dr * dr + dg * dg + db * db;
                }
                if (score < bestScore) { bestScore = score; best = i; }
            }

            Color32 chosen = cs[best];
            if (!ColorClose(chosen, fallbackBgKey, 80) && bestScore > 2000)
                return fallbackBgKey;

            return chosen;
        }

        private int GetMaxRadius(int[] radiusPxPerX)
        {
            int rMax = 0;
            for (int i = 0; i < radiusPxPerX.Length; i++)
                if (radiusPxPerX[i] > rMax) rMax = radiusPxPerX[i];
            return rMax;
        }

       
        // -------------------- Layout --------------------

        private void LayoutGost(
    RectInt inner,
    out RectInt profileRect, out RectInt frontRect,
    out RectInt colorRect, out RectInt stampRect)
        {
            int gap = MmToPxX(blockGapMm);

            // rezerwa na wymiary u góry
            int reserve = Mathf.Clamp(topViewDimReservePx, 0, inner.height / 3);
            int usableH = inner.height - reserve;
            if (usableH < 50) usableH = inner.height;

            int botH = (usableH - gap) / 2;
            int topH = usableH - botH - gap;

            int topY = inner.yMin + botH + gap;
            int topHWithReserve = topH + reserve;

            // góra: 2/3 + 1/3
            int w2 = (inner.width - gap) * 2 / 3;
            int w1 = (inner.width - gap) - w2;

            profileRect = new RectInt(inner.xMin, topY, w2, topHWithReserve);
            frontRect = new RectInt(inner.xMin + w2 + gap, topY, w1, topHWithReserve);

            // ✅ STEMPL stały: 185×55 mm
            int stampW = MmToPxX(185f);
            int stampH = MmToPxY(55f);

            // stempel w prawym dolnym rogu WEWNĘTRZNEJ ramki
            // int stampX = inner.xMax - stampW;
            int stampX = inner.xMin + inner.width - stampW;
            int stampY = inner.yMin;
            stampRect = new RectInt(stampX, stampY, stampW, stampH);

            int colorW = Mathf.Max(1, stampX - inner.xMin - gap);
            colorRect = new RectInt(inner.xMin, inner.yMin, colorW, botH);

            // stampRect = new RectInt(stampX, stampY, stampW, stampH);

            // ✅ widok kolorowy zajmuje pozostały dolny obszar po lewej stronie stempla

            colorRect = new RectInt(inner.xMin, inner.yMin, colorW, botH);
        }


        private int GetProfileDimReservePx()
        {
            // minimum: od linii wymiarowej do tekstu + linie pomocnicze + odstęp + zapas
            return dimGapPx + dimExtPx + dimTextOffsetPx + dimReserveExtraPx;
        }

        // ✅ uwzględniamy, że w profileRect dolna część jest zajęta przez wymiary
        private float ComputePxPerMmForProfileAndFront(
            RectInt profileRect, RectInt frontRect,
            float lengthMm, float diamMm,
            int padProfile, int padFront,
            int profileDimReservePx)
        {
            // dostępny obszar profilu (minus pad i minus rezerwa od dołu)
            int pw = Mathf.Max(1, profileRect.width - padProfile * 2);
            int ph = Mathf.Max(1, profileRect.height - padProfile * 2 - profileDimReservePx);

            float sProfileLen = pw / Mathf.Max(1e-6f, lengthMm);
            float sProfileDia = ph / Mathf.Max(1e-6f, diamMm);

            // przód: średnica mieści się w okręgu
            int fw = Mathf.Max(1, frontRect.width - padFront * 2);
            int fh = Mathf.Max(1, frontRect.height - padFront * 2);
            int fMin = Mathf.Min(fw, fh);

            float sFrontDia = fMin / Mathf.Max(1e-6f, diamMm);

            return Mathf.Min(sProfileLen, sProfileDia, sFrontDia);
        }

        // -------------------- Drawing primitives --------------------

        private void PutPixel(int x, int y, Color32 c)
        {
            if ((uint)x >= (uint)sheetWidth || (uint)y >= (uint)sheetHeight) return;
            _sheetPixels[y * sheetWidth + x] = c;
        }

        private void DrawLine(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                PutPixel(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void DrawLineThick(int x0, int y0, int x1, int y1, Color32 c, int thickness)
        {
            thickness = Mathf.Max(1, thickness);
            int half = thickness / 2;

            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                for (int oy = -half; oy <= half; oy++)
                    for (int ox = -half; ox <= half; ox++)
                        PutPixel(x0 + ox, y0 + oy, c);

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void DrawRect(RectInt r, Color32 c)
        {
            int x0 = r.xMin, x1 = r.xMax - 1;
            int y0 = r.yMin, y1 = r.yMax - 1;

            DrawLine(x0, y0, x1, y0, c);
            DrawLine(x1, y0, x1, y1, c);
            DrawLine(x1, y1, x0, y1, c);
            DrawLine(x0, y1, x0, y0, c);
        }

        private void DrawTextLine(int x, int y, string label)
        {
            // stary znacznik zostawiamy (szybko), ale w razie potrzeby można go zastąpić TMP
            // DrawLine(x, y, x + 120, y, axis);
        }

        // -------------------- Views (mm-aware) --------------------

        // ✅ dodaliśmy profileDimReservePx, aby rysować kontur wyżej i pozostawić miejsce na wymiary
        private void DrawProfileViewMm(
    RectInt rect, int[] radiusPx, int lengthPx,
    float mmPerPxX, float mmPerPxY, float pxPerMm,
    int profileDimReservePx,
    int forcedCenterY = int.MinValue)
        {
            int pad = 16;

            RectInt work = ShrinkRect(rect, 16, 16);

            // WAŻNE: rezerwa na dole jest przeznaczona dokładnie pod wymiar L w profilu
            work.height = Mathf.Max(10, work.height - profileDimReservePx);

            //int cy = work.yMin + work.height / 2;
            int cy = (forcedCenterY != int.MinValue) ? forcedCenterY : (work.yMin + work.height / 2);

            // oś w obszarze roboczym
            DrawLineThick(work.xMin + 10, cy, work.xMax - 10, cy, axis, axisLinePx);

            float lengthMm = (lengthPx - 1) * mmPerPxX;
            float drawLenPx = lengthMm * pxPerMm;

            int xStart = work.xMin + pad; // wyrównanie do lewej
            int xEnd = xStart + Mathf.RoundToInt(drawLenPx);

            int prevX = -1, prevTop = -1, prevBot = -1;
            int leftTop = 0, leftBot = 0;

            for (int i = 0; i < lengthPx; i++)
            {
                float xMm = i * mmPerPxX;
                float rMm = radiusPx[i] * mmPerPxY;

                int x = Mathf.RoundToInt(xStart + xMm * pxPerMm);
                int yTop = Mathf.RoundToInt(cy + rMm * pxPerMm);
                int yBot = Mathf.RoundToInt(cy - rMm * pxPerMm);

                if (i == 0) { leftTop = yTop; leftBot = yBot; }

                if (prevX >= 0)
                {
                    DrawLineThick(prevX, prevTop, x, yTop, ink, profileOutlinePx);
                    DrawLineThick(prevX, prevBot, x, yBot, ink, profileOutlinePx);
                }

                prevX = x;
                prevTop = yTop;
                prevBot = yBot;
            }

            
            DrawLineThick(xStart, leftBot, xStart, leftTop, ink, profileOutlinePx);
            DrawLineThick(prevX, prevBot, prevX, prevTop, ink, profileOutlinePx);

            // ===== Wymiar L (na dole) ====
            int xLeft = xStart;
            int xRight = prevX;

            // dolny punkt konturu
            int minY = int.MaxValue;
            for (int i = 0; i < lengthPx; i++)
            {
                float rMm = radiusPx[i] * mmPerPxY;
                int yBot = Mathf.RoundToInt(cy - rMm * pxPerMm);
                if (yBot < minY) minY = yBot;
            }

            // tekst
            float lengthMmReal = (lengthPx - 1) * mmPerPxX;
            string sText = $"{lengthMmReal:0.0}mm";

            // ✅ chcemy niżej
            int extraDown = dimExtPx + 6; // <-- zwiększ, jeśli potrzebne jest jeszcze niżej

            int yDimWanted = minY - dimGapPx - dimExtPx - extraDown;

            // granice: na dole nie wychodzimy poza rect, u góry nie wchodzimy w obszar profilu
            int yDimMin = rect.yMin + 6;
            int yDimMax = work.yMin - 6;
            int yDim = Mathf.Clamp(yDimWanted, yDimMin, yDimMax);

            // linie pomocnicze
            DrawLineThick(xLeft, cy, xLeft, yDim, axis, dimLinePx);
            DrawLineThick(xRight, cy, xRight, yDim, axis, dimLinePx);

            // strzałki
            DrawArrowHead(xLeft, yDim, +1, dimArrowSizePx, axis);
            DrawArrowHead(xRight, yDim, -1, dimArrowSizePx, axis);

            // ✅ automatyczny rozmiar tekstu (2 lub 1)
            int scaleText = 2;
            int textW2 = MeasureSegStringWidth(sText, 2);
            int textW1 = MeasureSegStringWidth(sText, 1);

            // wolna przestrzeń między strzałkami (aby tekst się nie stykał)
            int freeW = Mathf.Abs(xRight - xLeft) - (dimArrowSizePx * 2 + 10);

            // jeśli przy scale = 2 się nie mieści — zmniejszamy
            if (textW2 > freeW) scaleText = 1;

            // szerokość tekstu dla wybranego scale
            int textW = (scaleText == 2) ? textW2 : textW1;

            // przerwa w linii pod tekstem (aby tekst był „na linii”)
            int midX = (xLeft + xRight) / 2;
            int gap = 6; // odstęp wokół tekstu
            int cutL = midX - textW / 2 - gap;
            int cutR = midX + textW / 2 + gap;

            // sama linia wymiarowa z przerwą
            DrawLineThick(xLeft, yDim, cutL, yDim, axis, dimLinePx);
            DrawLineThick(cutR, yDim, xRight, yDim, axis, dimLinePx);

            // ✅ tekst bezpośrednio na linii
            // DrawDimTextOnSheet(midX, yDim + 2, sText, axis, scaleText);
            DrawDimTextOnSheet(midX, yDim + 2, sText, dimTextColor, scaleText);
        }

        
        private void DrawCircleThick(int cx, int cy, int r, int thickness, Color32 c)
        {
            thickness = Mathf.Max(1, thickness);
            int half = thickness / 2;

            for (int rr = r - half; rr <= r + half; rr++)
            {
                if (rr > 0) DrawCircle(cx, cy, rr, c);
            }
        }

        private void DrawCircle(int cx, int cy, int r, Color32 c)
        {
            int x = r;
            int y = 0;
            int err = 0;

            while (x >= y)
            {
                PutPixel(cx + x, cy + y, c);
                PutPixel(cx + y, cy + x, c);
                PutPixel(cx - y, cy + x, c);
                PutPixel(cx - x, cy + y, c);
                PutPixel(cx - x, cy - y, c);
                PutPixel(cx - y, cy - x, c);
                PutPixel(cx + y, cy - x, c);
                PutPixel(cx + x, cy - y, c);

                y++;
                if (err <= 0) err += 2 * y + 1;
                if (err > 0) { x--; err -= 2 * x + 1; }
            }
        }

        // -------------------- Arrow (single version, no overload conflicts) --------------------
        private void DrawArrowHead(int xTip, int yTip, int dirX, int size, Color32 c)
        {
            DrawLineThick(xTip, yTip, xTip + dirX * size, yTip + size / 2, c, dimLinePx);
            DrawLineThick(xTip, yTip, xTip + dirX * size, yTip - size / 2, c, dimLinePx);
        }

        // -------------------- Color blit --------------------

        private void BlitSpriteToRect_ColorKey(
    Sprite sprite,
    RectInt dst,
    int cropLeftPx,
    Color32 bgKey,
    bool keepAspect = true,
    float forcedAspect = -1f,
    int forcedOx = int.MinValue
)
        {
            if (!sprite) return;

            Texture2D tex = sprite.texture;
            Rect sr = sprite.rect;

            int sw = (int)sr.width;
            int sh = (int)sr.height;
            int sx0 = (int)sr.x;
            int sy0 = (int)sr.y;

            cropLeftPx = Mathf.Clamp(cropLeftPx, 0, Mathf.Max(0, sw - 1));

            int srcW = sw - cropLeftPx;
            int srcH = sh;
            if (srcW <= 0 || srcH <= 0) return;

            int dw = dst.width;
            int dh = dst.height;

            int tw = dw, th = dh;

            if (keepAspect)
            {
                float sAspect = (forcedAspect > 0f) ? forcedAspect : (srcW / (float)srcH);
                float dAspect = dw / (float)dh;

                if (sAspect > dAspect) th = Mathf.RoundToInt(dw / sAspect);
                else tw = Mathf.RoundToInt(dh * sAspect);
            }


            int ox = (forcedOx != int.MinValue) ? forcedOx : (dst.xMin + (dw - tw) / 2);
            int oy = dst.yMin + (dh - th) / 2;

            Color32[] pixels;
            try { pixels = tex.GetPixels32(); }
            catch { return; }

            for (int y = 0; y < th; y++)
            {
                int sy = Mathf.Clamp(
                    Mathf.RoundToInt((y / (float)Mathf.Max(1, th - 1)) * (srcH - 1)),
                    0, srcH - 1
                );

                for (int x = 0; x < tw; x++)
                {
                    int sx = Mathf.Clamp(
                        Mathf.RoundToInt((x / (float)Mathf.Max(1, tw - 1)) * (srcW - 1)),
                        0, srcW - 1
                    );

                    Color32 c = pixels[(sy0 + sy) * tex.width + (sx0 + cropLeftPx + sx)];

                    if (!IsMaterial(c, bgKey)) continue;

                    //PutPixel(ox + x, oy + y, c);
                    PutPixel(ox + x, oy + y, MultiplyRgb(c, _materialTint));
                }
            }
        }

        // -------------------- GOST frame + stamp --------------------

        private void DrawGostFrame()
        {
            // --- RAMKA ARKUSZA (20 mm z lewej, 5 mm pozostałe strony) ---
            int L = MmToPxX(marginLeftMm);
            int R = sheetWidth - 1 - MmToPxX(marginOtherMm);
            int B = MmToPxY(marginOtherMm);
            int T = sheetHeight - 1 - MmToPxY(marginOtherMm);

            DrawLine(L, B, R, B, ink);
            DrawLine(R, B, R, T, ink);
            DrawLine(R, T, L, T, ink);
            DrawLine(L, T, L, B, ink);

            // --- STEMPL GOST: prostokąt 185 × 55 mm (wewnątrz ramki, prawy dolny róg) ---
            int stampW = MmToPxX(185f);
            int stampH = MmToPxY(55f);

            int stampX = R - stampW + 1; // +1, ponieważ R jest już „inkluzywnym” pikselem
            int stampY = B;

            var stampRect = new RectInt(stampX, stampY, stampW, stampH);

            // Etap 1: tylko ramka stempla + X = 130 + Y = 15 (od dołu stempla)
            DrawGostStamp_Step2(stampRect);

        }

        private RectInt GetInnerFrameRect()
        {
            int L = MmToPxX(marginLeftMm);
            int R = sheetWidth - 1 - MmToPxX(marginOtherMm);
            int B = MmToPxY(marginOtherMm);
            int T = sheetHeight - 1 - MmToPxY(marginOtherMm);

            return new RectInt(L, B, (R - L + 1), (T - B + 1));
        }



        // -------------------- Rect helpers --------------------

        private RectInt ShrinkRect(RectInt r, int padX, int padY)
        {
            int x = r.xMin + padX;
            int y = r.yMin + padY;
            int w = Mathf.Max(1, r.width - padX * 2);
            int h = Mathf.Max(1, r.height - padY * 2);
            return new RectInt(x, y, w, h);
        }

        // -------------------- TMP labels pool --------------------

        private void EnsureLabelPool()
        {
           // if (!labelsRoot || !labelPrefab) return;

            /*while (_labelPool.Count < labelPoolStart)
            {
                var t = Instantiate(labelPrefab, labelsRoot);
                t.gameObject.SetActive(false);
                _labelPool.Add(t);
            }*/
        }


        /// <summary>
        //// pxX / pxY — współrzędne na arkuszu w pikselach (jak w PutPixel), gdzie (0,0) = lewy dolny róg.
        /// </summary>

        public void ShowSingleViewWithDims(SpriteRenderer cutPartSR, Vector3 stableLossyScale)
        {
            if (!cutPartSR || !cutPartSR.sprite)
            {
                Debug.LogWarning("[DrawingUI] ShowSingleViewWithDims: SpriteRenderer/Sprite missing.");
                return;
            }

            EnsureSheet();
            ClearSheet(paper);
           // ClearLabels();

            _pxPerMmX = sheetWidth / Mathf.Max(1f, sheetWidthMm);
            _pxPerMmY = sheetHeight / Mathf.Max(1f, sheetHeightMm);

            DrawGostFrame();
            RectInt inner = GetInnerFrameRect();

            Sprite sprite = cutPartSR.sprite;

            // Profil + przycięcie z lewej strony
            if (!TryExtractProfile(sprite, out int[] radiusPxPerX, out int matLenPx, out int cropLeftPx, out Color32 bgKeyUsed))
            {
                if (infoText) infoText.text = "Nie można odczytać profilu.";
                ApplySheet();
                if (drawingPanel) drawingPanel.SetActive(true);
                return;
            }

            int rMaxPx = GetMaxRadius(radiusPxPerX);
            if (rMaxPx <= 0 || matLenPx <= 2)
            {
                if (infoText) infoText.text = "Profil пустой.";
                ApplySheet();
                if (drawingPanel) drawingPanel.SetActive(true);
                return;
            }

            // Wymiary WORLD na podstawie pikseli profilu (jak u Ciebie), ale skala jest stała
            float ppu = Mathf.Max(1e-6f, sprite.pixelsPerUnit);
            Vector3 s = stableLossyScale;

            float lengthWorld = (matLenPx / ppu) * Mathf.Abs(s.x);
            float diamWorld = ((2f * rMaxPx) / ppu) * Mathf.Abs(s.y);

            float lengthMm = lengthWorld / Mathf.Max(1e-6f, _unitsPerMmX);
            float diamMm = diamWorld / Mathf.Max(1e-6f, _unitsPerMmY);

            // Układ: profil (lewy górny), przód (prawy górny), kolor (lewy dolny), stempel (prawy dolny)
            LayoutGost(inner, out RectInt profileRect, out RectInt frontRect, out RectInt colorRect, out RectInt stampRect);

            DrawTextLine(profileRect.xMin + 10, profileRect.yMax - 16, "WIDOK Z BOKU");
            DrawTextLine(frontRect.xMin + 10, frontRect.yMax - 16, "WIDOK Z PRZODU");
            DrawTextLine(colorRect.xMin + 10, colorRect.yMax - 16, "WIDOK (KOLOR)");

            // ✅ skala z uwzględnieniem rezerwy na dole w profileRect pod wymiar L
            int dimReservePx = GetProfileDimReservePx();
            float pxPerMm = ComputePxPerMmForProfileAndFront(profileRect, frontRect, lengthMm, diamMm, 16, 18, dimReservePx);

            // px -> mm (dla profilu)
            float mmPerPxX = lengthMm / Mathf.Max(1, matLenPx - 1);
            float mmPerPxY = diamMm / Mathf.Max(1, 2 * rMaxPx);


            int sharedCenterY = profileRect.yMin + profileRect.height / 2;

            DrawProfileViewMm(profileRect, radiusPxPerX, matLenPx, mmPerPxX, mmPerPxY, pxPerMm, dimReservePx, sharedCenterY);
            // DrawFrontViewMm(frontRect, diamMm, pxPerMm, sharedCenterY);

            DrawFrontViewLevelsMm(frontRect, radiusPxPerX, mmPerPxY, pxPerMm, sharedCenterY);

            // widok kolorowy — w rzeczywistych proporcjach świata
            //RectInt dstColor = ShrinkRect(colorRect, innerPadPx, innerPadPx);
            float worldAspect = lengthMm / Mathf.Max(1e-6f, diamMm);
          
            RectInt dstColor = ShrinkRect(colorRect, innerPadPx, innerPadPx);

            int commonLeftX = profileRect.xMin + leftAlignPadPx;

            BlitSpriteToRect_ColorKey(
                sprite, dstColor, cropLeftPx, bgKeyUsed,
                keepAspect: true,
                forcedAspect: worldAspect,
                forcedOx: commonLeftX
            );


            /*if (infoText)
            {
                infoText.text =
                    $"L={lengthMm:0.0}mm\n" +
                    $"Ø={diamMm:0.0}mm\n" +
                    $"matLenPx={matLenPx}\n" +
                    $"rMaxPx={rMaxPx}\n" +
                    $"ppu={ppu:0.###}\n" +
                    $"stableScale=({s.x:0.###},{s.y:0.###})\n" +
                    $"uPerMmX={_unitsPerMmX:0.######}\n" +
                    $"uPerMmY={_unitsPerMmY:0.######}\n" +
                    $"MaterialName={_materialName}\n";
            }*/

           // Debug.Log($"[DRAW] Lw={lengthWorld:F3} Dw={diamWorld:F3} => L={lengthMm:F1} Ø={diamMm:F1}");

            //DrawStampText(stampRect, _materialName, lengthMm, diamMm);
            UpdateStampText(_materialName, _nominalLenMm, _nominalDiamMm);

            ApplySheet();
            if (drawingPanel) drawingPanel.SetActive(true);
        }


        // ===================== helpers for ShowSingleViewWithDims =====================
        // ===================== TEXT ON TEXTURE (no TMP) =====================

        private void DrawDimTextOnSheet(int centerX, int centerY, string text, Color32 c, int scale = 2)
        {
            if (string.IsNullOrEmpty(text)) return;

            scale = Mathf.Max(1, scale);

            // widok kolorowy — w rzeczywistych proporcjach świata
            int w = MeasureSegStringWidth(text, scale);
            int x = centerX - w / 2;
            int y = centerY - (7 * scale) / 2;

            DrawSegString(x, y, text, c, scale);
        }

        private int MeasureSegStringWidth(string s, int scale)
        {
            int w = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                w += MeasureSegCharWidth(ch, scale);
                if (i != s.Length - 1) w += 2 * scale; // gap
            }
            return w;
        }

        private int MeasureSegCharWidth(char ch, int scale)
        {
            if (ch == ' ') return 4 * scale;
            if (ch == '.') return 3 * scale;
            if (ch == ',') return 3 * scale;
            if (ch == '-') return 5 * scale;
            // cyfry i litery
            return 6 * scale;
        }

        private void DrawSegString(int x, int y, string s, Color32 c, int scale)
        {
            int cx = x;
            for (int i = 0; i < s.Length; i++)
            {
                DrawSegChar(cx, y, s[i], c, scale);
                cx += MeasureSegCharWidth(s[i], scale) + 2 * scale;
            }
        }

        /// <summary>
        /// styl 7-seg + kilka liter (m) + symbole '.', ',', '-'
        /// Współrzędna (x, y) — lewy dolny róg znaku.
        /// Wysokość znaku ~ 7*scale, szerokość ~ 6*scale.
        /// </summary>
        private void DrawSegChar(int x, int y, char ch, Color32 c, int scale)
        {
            ch = char.ToLowerInvariant(ch);

            // kropki / przecinki
            if (ch == '.' || ch == ',')
            {
                PutPixel(x + 2 * scale, y, c);
                PutPixel(x + 2 * scale + 1, y, c);
                if (ch == ',') PutPixel(x + 2 * scale, y - 1, c);
                return;
            }

            if (ch == ' ')
                return;

            if (ch == '-')
            {
                DrawH(x + 1 * scale, y + 3 * scale, 4 * scale, c, scale);
                return;
            }

            // litery: wykonamy w prosty sposób
            if (ch == 'm')
            {
                // litera „m” jako trzy pionowe linie + dwa „łuki”
                DrawV(x + 0 * scale, y + 0 * scale, 6 * scale, c, scale);
                DrawV(x + 2 * scale, y + 0 * scale, 6 * scale, c, scale);
                DrawV(x + 5 * scale, y + 0 * scale, 6 * scale, c, scale);
                DrawH(x + 0 * scale, y + 5 * scale, 2 * scale, c, scale);
                DrawH(x + 2 * scale, y + 5 * scale, 3 * scale, c, scale);
                return;
            }

            // pozostałe litery (np. „m” w „mm”) można zminimalizować:
            // jeśli to „m” lub „n” — wystarczy. Dla „m” już jest.
            if (ch == 'n')
            {
                DrawV(x + 0 * scale, y + 0 * scale, 6 * scale, c, scale);
                DrawV(x + 5 * scale, y + 0 * scale, 6 * scale, c, scale);
                DrawH(x + 0 * scale, y + 5 * scale, 5 * scale, c, scale);
                return;
            }

            // cyfry realizowane przez 7-seg
            int seg = ch switch
            {
                '0' => SegA | SegB | SegC | SegD | SegE | SegF,
                '1' => SegB | SegC,
                '2' => SegA | SegB | SegG | SegE | SegD,
                '3' => SegA | SegB | SegG | SegC | SegD,
                '4' => SegF | SegG | SegB | SegC,
                '5' => SegA | SegF | SegG | SegC | SegD,
                '6' => SegA | SegF | SegG | SegC | SegD | SegE,
                '7' => SegA | SegB | SegC,
                '8' => SegA | SegB | SegC | SegD | SegE | SegF | SegG,
                '9' => SegA | SegB | SegC | SegD | SegF | SegG,
                _ => 0
            };

            if (seg == 0) return;

            // Segmenty:
            // A: góra
            // B: prawy górny
            // C: prawy dolny
            // D: dół
            // E: lewy dolny
            // F: lewy górny
            // G: środek

            // rozmiar znaku: width = 6*scale, height = 7*scale
            int w = 6 * scale;
            int h = 7 * scale;

            // grubość segmentu
            int t = Mathf.Max(1, scale);

            if ((seg & SegA) != 0) DrawH(x + t, y + h - t, w - 2 * t, c, t);
            if ((seg & SegD) != 0) DrawH(x + t, y + 0 * scale, w - 2 * t, c, t);
            if ((seg & SegG) != 0) DrawH(x + t, y + (h / 2), w - 2 * t, c, t);

            if ((seg & SegF) != 0) DrawV(x + 0 * scale, y + (h / 2), h / 2, c, t);
            if ((seg & SegE) != 0) DrawV(x + 0 * scale, y + 0 * scale, h / 2, c, t);

            if ((seg & SegB) != 0) DrawV(x + w - t, y + (h / 2), h / 2, c, t);
            if ((seg & SegC) != 0) DrawV(x + w - t, y + 0 * scale, h / 2, c, t);
        }

        private void DrawH(int x, int y, int len, Color32 c, int thickness)
        {
            for (int i = 0; i < len; i++)
                for (int t = 0; t < thickness; t++)
                    PutPixel(x + i, y + t, c);
        }

        private void DrawV(int x, int y, int len, Color32 c, int thickness)
        {
            for (int i = 0; i < len; i++)
                for (int t = 0; t < thickness; t++)
                    PutPixel(x + t, y + i, c);
        }

        // Segmenty: 7-seg
        private const int SegA = 1 << 0;
        private const int SegB = 1 << 1;
        private const int SegC = 1 << 2;
        private const int SegD = 1 << 3;
        private const int SegE = 1 << 4;
        private const int SegF = 1 << 5;
        private const int SegG = 1 << 6;


        private void DrawCircleDashed(int cx, int cy, int r, int dashLen = 10, int gapLen = 6, Color32 c = default)
        {
            if (r <= 1) return;
            if (c.a == 0) c = axis;

            // rysujemy wzdłuż narożnika
            float step = 1f / Mathf.Max(1, r); // im większy promień, tym więcej punktów
            int counter = 0;
            bool draw = true;
            int segLeft = dashLen;

            for (float a = 0; a < Mathf.PI * 2f; a += step)
            {
                int x = cx + Mathf.RoundToInt(Mathf.Cos(a) * r);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(a) * r);

                if (draw) PutPixel(x, y, c);

                segLeft--;
                if (segLeft <= 0)
                {
                    draw = !draw;
                    segLeft = draw ? dashLen : gapLen;
                }

                counter++;
                if (counter > 200000) break; // zabezpieczenie
            }
        }

        private void DrawFrontViewLevelsMm(
    RectInt rect,
    int[] radiusPxPerX,
    float mmPerPxY,
    float pxPerMm,
    int forcedCenterY = int.MinValue)
        {
            int pad = 18;

            int fw = rect.width - pad * 2;
            int fh = rect.height - pad * 2;
            int size = Mathf.Min(fw, fh);
            if (size < 10) return;

            int cx = rect.xMin + pad + size / 2;
            int cy = (forcedCenterY != int.MinValue) ? forcedCenterY : (rect.yMin + pad + size / 2);

            // osie
            int axisR = size / 2 - 6;
            DrawLineThick(cx - axisR, cy, cx + axisR, cy, axis, axisLinePx);
            DrawLineThick(cx, cy - axisR, cx, cy + axisR, axis, axisLinePx);

            // poziomy promieni (na całym profilu)
            var levelsPx = GetRadiusLevels(radiusPxPerX);
            if (levelsPx.Count == 0) return;

            // które promienie są widoczne dokładnie na prawym czole
            //var visibleSet = GetVisibleRadiiFromRightEnd(radiusPxPerX, sampleCols: 8, tol: 2);
            var visibleSet = GetVisibleRadiiFromRightEnd(radiusPxPerX, searchBackCols: 80, tol: 2);

            // maksymalny = zewnętrzny
            int rMaxPx = levelsPx[0];
            float rMaxMm = rMaxPx * mmPerPxY;
            int rMaxDraw = Mathf.RoundToInt(rMaxMm * pxPerMm);

            // zewnętrzny zawsze linią ciągłą
            DrawCircleThick(cx, cy, rMaxDraw, circleOutlinePx, ink);

            // pozostałe: jeśli promień występuje na prawym czole -> linia ciągła, w przeciwnym razie -> linia przerywana
            for (int i = 0; i < levelsPx.Count; i++)
            {
                int rLevelPx = levelsPx[i];
                float rMm = rLevelPx * mmPerPxY;
                int r = Mathf.RoundToInt(rMm * pxPerMm);
                if (r <= 2) continue;

                bool isVisible = IsRadiusVisibleFromRight(radiusPxPerX, rLevelPx, tol: 1);

                if (isVisible)
                    DrawCircleThick(cx, cy, r, visibleInnerCirclePx, ink);
                else
                    DrawCircleDashed(cx, cy, r, dashLen: 10, gapLen: 6, c: ink);
            }


            // ✅ wyprowadzenie Ømax (na dole)
            float diamMaxMm = rMaxMm * 2f;
            //DrawDiameterDimension(rect, cx, cy, rMaxDraw, diamMaxMm, forcedCenterY);
            DrawDiameterDimension(rect, cx, cy, rMaxDraw, diamMaxMm);
        }

        private void DrawDiameterDimension(RectInt rect, int cx, int cy, int rPx, float diamMm)
        {
            // punkty średnicy w osi X
            int xL = cx - rPx;
            int xR = cx + rPx;

            // bazowa wysokość dla wymiaru (na dole bloku)
            int yDimWanted = rect.yMin + 12;

            // aby linia była poniżej okręgu
            int yFrom = cy; // - rPx;
            int yDim = Mathf.Min(yDimWanted, yFrom - 10);
            yDim = Mathf.Max(rect.yMin + 6, yDim);

            // linie pomocnicze
            DrawLineThick(xL, yFrom, xL, yDim, axis, dimLinePx);
            DrawLineThick(xR, yFrom, xR, yDim, axis, dimLinePx);

            // tekst
            string sText = $"Ø{diamMm:0.0}mm";

            // automatyczny scale tekstu: 2 jeśli się mieści, w przeciwnym razie 1
            int scaleText = 2;
            int textW2 = MeasureSegStringWidth(sText, 2);
            int textW1 = MeasureSegStringWidth(sText, 1);

            int freeW = Mathf.Abs(xR - xL) - (dimArrowSizePx * 2 + 10);
            if (textW2 > freeW) scaleText = 1;

            int textW = (scaleText == 2) ? textW2 : textW1;

            int midX = (xL + xR) / 2;

            // przerwa w linii pod tekstem
            int gap = 6;
            int cutL = midX - textW / 2 - gap;
            int cutR = midX + textW / 2 + gap;

            // linia wymiarowa z przerwą
            DrawLineThick(xL, yDim, cutL, yDim, axis, dimLinePx);
            DrawLineThick(cutR, yDim, xR, yDim, axis, dimLinePx);

            // strzałki do wewnątrz
            DrawArrowHead(xL, yDim, +1, dimArrowSizePx, axis);
            DrawArrowHead(xR, yDim, -1, dimArrowSizePx, axis);

            // tekst na linii
            DrawDimTextOnSheet(midX, yDim + 2, sText, ink, scaleText);
        }

        // zbieramy unikalne poziomy promieni (stopnie) na profilu
        private List<int> GetRadiusLevels(int[] radiusPxPerX)
        {
            var list = new List<int>();
            if (radiusPxPerX == null || radiusPxPerX.Length == 0) return list;

            const int tol = 2; // tolerancja, aby zbliżone promienie były traktowane jako jeden poziom

            for (int i = 0; i < radiusPxPerX.Length; i++)
            {
                int r = radiusPxPerX[i];
                if (r <= 0) continue;

                bool exists = false;
                for (int k = 0; k < list.Count; k++)
                {
                    if (Mathf.Abs(list[k] - r) <= tol) { exists = true; break; }
                }

                if (!exists) list.Add(r);
            }

            // sortujemy malejąco (pierwszy — maksymalna średnica)
            list.Sort((a, b) => b.CompareTo(a));
            return list;
        }

        // promienie, które faktycznie „wychodzą” na prawe czoło (widoczne przy patrzeniu z prawej do lewej)
        private HashSet<int> GetVisibleRadiiFromRightEnd(int[] radiusPxPerX, int searchBackCols = 60, int tol = 2)
        {
            var set = new HashSet<int>();
            if (radiusPxPerX == null || radiusPxPerX.Length == 0) return set;

            int n = radiusPxPerX.Length;

            // szukamy ostatniego niezerowego (rzeczywistego czoła materiału)
            int last = -1;
            for (int i = n - 1; i >= 0; i--)
            {
                if (radiusPxPerX[i] > 0) { last = i; break; }
            }
            if (last < 0) return set;

            int from = Mathf.Max(0, last - searchBackCols);

            // zbieramy poziomy promieni w strefie w pobliżu czoła
            for (int i = from; i <= last; i++)
            {
                int r = radiusPxPerX[i];
                if (r <= 0) continue;

                bool merged = false;
                foreach (var existing in set)
                {
                    if (Mathf.Abs(existing - r) <= tol) { merged = true; break; }
                }
                if (!merged) set.Add(r);
            }

            return set;
        }


        private bool IsRadiusVisibleFromRight(int[] radiusPxPerX, int radiusPx, int tol = 1)
        {
            int n = radiusPxPerX.Length;

            // szukamy wszystkich pozycji, w których występuje ten promień
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(radiusPxPerX[i] - radiusPx) <= tol)
                {
                    // sprawdzamy wszystko PO PRAWEJ stronie
                    for (int j = i + 1; j < n; j++)
                    {
                        if (radiusPxPerX[j] > radiusPx + tol)
                            return false; // zasłonięty przez większą średnicę
                    }
                }
            }

            return true; // nigdzie nie jest zasłonięty → widoczny
        }

        
        private void DrawGostStamp_Step2(RectInt r)
        {
            int x0 = r.xMin;
            int x1 = r.xMax - 1;
            int y0 = r.yMin;
            int y1 = r.yMax - 1;

            DrawRect(r, ink);

            float sx = r.width / 185f;
            float sy = r.height / 55f;

            int Xmm(float mm) => x0 + Mathf.RoundToInt(mm * sx);
            int Ymm(float mm) => y0 + Mathf.RoundToInt(mm * sy);

            // ===== Punkty X (mm) =====
            int x7 = Xmm(7);
            int x17 = Xmm(17);
            int x40 = Xmm(40);
            int x55 = Xmm(55);
            int x65 = Xmm(65);
            int x135 = Xmm(135);

            int x140 = Xmm(140);
            int x145 = Xmm(145);
            int x150 = Xmm(150);
            int x155 = Xmm(155);
            int x167 = Xmm(167);
            int x185 = Xmm(185); // formalnie; prawa granica = x1

            // ===== Punkty Y (mm od DOŁU) =====
            int y5 = Ymm(5);
            int y10 = Ymm(10);
            int y15 = Ymm(15);
            int y20 = Ymm(20);
            int y25 = Ymm(25);
            int y30 = Ymm(30);
            int y35 = Ymm(35);
            int y40 = Ymm(40);
            int y45 = Ymm(45);
            int y50 = Ymm(50);
            int y55 = Ymm(55); // góra; faktycznie = y1

            // ===== LINIE GŁÓWNE (jak zostały wymienione) =====

            // Linie poziome na całą szerokość
            DrawLineThick(x0, y55, x1, y55, ink, 2);
            DrawLineThick(x0, y0, x1, y0, ink, 2);
            DrawLineThick(x0, y0, x0, y55, ink, 2);
            DrawLineThick(x1, y0, x1, y55, ink, 2);



            DrawLineThick(x65, y15, x1, y15, ink, 2);
            DrawLineThick(x65, y40, x1, y40, ink, 2);

            // Linie pionowe po lewej stronie
            DrawLineThick(x7, y55, x7, y30, ink, 2);
            DrawLineThick(x17, y0, x17, y55, ink, 2);
            DrawLineThick(x40, y0, x40, y55, ink, 2);
            DrawLineThick(x55, y0, x55, y55, ink, 2);
            DrawLineThick(x65, y0, x65, y55, ink, 2);

            // Prawy blok
            DrawLineThick(x135, y0, x135, y40, ink, 2);
            DrawLineThick(x135, y20, x1, y20, ink, 2);
            DrawLineThick(x135, y35, x1, y35, ink, 2);

            // Wąskie linie pionowe wewnątrz prawego bloku
            DrawLine(x140, y20, x140, y35, ink);
            DrawLine(x145, y20, x145, y35, ink);
            DrawLineThick(x150, y20, x150, y40, ink, 2);
            DrawLineThick(x167, y20, x167, y40, ink, 2);
            DrawLineThick(x155, y15, x155, y20, ink, 2);


            // ===== „końcowe” linie poziome po lewej stronie (0..65) =====
            DrawLine(x0, y5, x65, y5, ink);
            DrawLine(x0, y10, x65, y10, ink);
            DrawLine(x0, y15, x65, y15, ink);
            DrawLine(x0, y20, x65, y20, ink);
            DrawLine(x0, y25, x65, y25, ink);
            DrawLineThick(x0, y30, x65, y30, ink, 2);
            DrawLineThick(x0, y35, x65, y35, ink, 2);
            DrawLine(x0, y40, x65, y40, ink);
            DrawLine(x0, y45, x65, y45, ink);
            DrawLine(x0, y50, x65, y50, ink);
        }

        public void RequestClose()
        {
            CloseRequested?.Invoke();
        }

        private void Awake()
        {
            if (okButton) okButton.onClick.AddListener(OnOkClicked);
            if (cancelButton) cancelButton.onClick.AddListener(OnCancelClicked);
            if (downloadPngButton) downloadPngButton.onClick.AddListener(OnDownloadPngButton);
        }

        private void OnDestroy()
        {
            if (okButton) okButton.onClick.RemoveListener(OnOkClicked);
            if (cancelButton) cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (downloadPngButton) downloadPngButton.onClick.RemoveListener(OnDownloadPngButton);
        }

        private void OnOkClicked()
        {
            CloseRequested?.Invoke();
        }

        private void OnCancelClicked()
        {
            CloseRequested?.Invoke();
        }

        public void ShowPanel()
        {
            if (drawingPanel) drawingPanel.SetActive(true);
        }

        private IEnumerator CapturePanelCoroutine()
        {
            // aby UI na pewno się odświeżył (również TMP)
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            RectTransform rt = captureRect != null ? captureRect : sheetImage.rectTransform;

            // współrzędne w pikselach ekranowych
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            // corners: 0=BL, 1=TL, 2=TR, 3=BR
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

            int x = Mathf.RoundToInt(bl.x);
            int y = Mathf.RoundToInt(bl.y);
            int w = Mathf.RoundToInt(tr.x - bl.x);
            int h = Mathf.RoundToInt(tr.y - bl.y);

            if (w <= 2 || h <= 2)
            {
                Debug.LogWarning("[PNG] Capture rect too small.");
                _sheetTex = null;
                yield break;
            }

            // na wszelki wypadek ograniczamy do granic ekranu
            x = Mathf.Clamp(x, 0, Screen.width - 1);
            y = Mathf.Clamp(y, 0, Screen.height - 1);
            w = Mathf.Clamp(w, 1, Screen.width - x);
            h = Mathf.Clamp(h, 1, Screen.height - y);

            // Odczytujemy piksele z ekranu (Canvas typu Overlay jest odpowiedni!)
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(x, y, w, h), 0, 0, false);
            tex.Apply(false, false);

            _sheetTex = tex; // ✅ teraz GetSheetTextureCopy zwróci wersję „z napisami”
        }

        public void OnDownloadPngButton()
        {
            if (_captureRoutine != null) StopCoroutine(_captureRoutine);
            _captureRoutine = StartCoroutine(CaptureThenRequestDownload());

            //StartCoroutine(CaptureThenRequestDownload());
        }

       /* private IEnumerator DownloadFlow()
        {
            yield return CapturePanelCoroutine();   // ✅ wykonujemy zrzut UI
            DownloadPngRequested?.Invoke();         // ✅ dalej zapis jak u Ciebie
            Debug.Log("Downloads");
        }*/

        public Texture2D CaptureSheetWithUI()
        {
            if (uiCamera == null || sheetRect == null) return null;

            // rect na ekranie (w pikselach)
            Vector3[] corners = new Vector3[4];
            sheetRect.GetWorldCorners(corners);

            Vector3 bl = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]); // bottom-left
            Vector3 tr = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]); // top-right

            int w = Mathf.RoundToInt(tr.x - bl.x);
            int h = Mathf.RoundToInt(tr.y - bl.y);

            if (w <= 2 || h <= 2) return null;

            var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            var prev = uiCamera.targetTexture;

            uiCamera.targetTexture = rt;
            uiCamera.Render();

            RenderTexture.active = rt;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(bl.x, bl.y, w, h), 0, 0);
            tex.Apply(false, false);

            uiCamera.targetTexture = prev;
            RenderTexture.active = null;
            Destroy(rt);

            return tex;
        }

        public Texture2D GetSheetTextureCopy()
        {
            if (_sheetTex_Download == null) return null;

            var copy = new Texture2D(_sheetTex_Download.width, _sheetTex_Download.height, TextureFormat.RGBA32, false);
            copy.SetPixels32(_sheetTex_Download.GetPixels32());
            copy.Apply(false, false);
            return copy;
        }

        private static Texture2D CaptureRectToTexture(RectTransform rt, Canvas canvas, Camera cam)
        {
            if (!rt || !canvas)
            {
                Debug.LogWarning("[PNG] CaptureRect/Canvas not assigned.");
                return null;
            }

            // Dla Screen Space Overlay kamera powinna być ustawiona na null
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                cam = null;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]); // bottom-left
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]); // top-right

            int x = Mathf.RoundToInt(bl.x);
            int y = Mathf.RoundToInt(bl.y);
            int w = Mathf.RoundToInt(tr.x - bl.x);
            int h = Mathf.RoundToInt(tr.y - bl.y);

            // zabezpieczenie przed nietypowymi wartościami
            w = Mathf.Clamp(w, 1, Screen.width);
            h = Mathf.Clamp(h, 1, Screen.height);

            // przycięcie do granic ekranu
            x = Mathf.Clamp(x, 0, Screen.width - w);
            y = Mathf.Clamp(y, 0, Screen.height - h);

            if (w < 4 || h < 4)
            {
                Debug.LogWarning("[PNG] Capture rect too small.");
                return null;
            }

            // wykonujemy zrzut całego ekranu
            Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();
            if (full == null) return null;

            // wycinamy tylko prostokąt panelu
            Texture2D cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cropped.SetPixels(full.GetPixels(x, y, w, h));
            cropped.Apply(false, false);

            UnityEngine.Object.Destroy(full);
            return cropped;
        
        }

        public void SetButtonsVisible(bool visible)
        {

            Debug.Log($"[PNG] buttonsRoot = {(buttonsRoot ? buttonsRoot.name : "NULL")}, setActive={visible}");
            if (buttonsRoot != null)
                buttonsRoot.SetActive(visible);
        }

        private IEnumerator CaptureThenRequestDownload()
        {
            // 1) Ukrywamy przyciski PRZED klatką renderowania
            SetButtonsVisible(false);

            // 2) Pozwalamy UI faktycznie się przerysować
            Canvas.ForceUpdateCanvases();
            yield return null;                  // 1 klatka
            yield return new WaitForEndOfFrame(); // 2 klatka (pewniejsze)

            // 3) Przechwycenie
            _sheetTex_Download = CaptureRectToTexture(captureRect, drawingCanvas, uiCamera);

            // 4) Przywracamy przyciski
            SetButtonsVisible(true);

            if (_sheetTex_Download == null)
            {
                Debug.LogWarning("[PNG] No captured UI texture to save.");
                yield break;
            }

            DownloadPngRequested?.Invoke();
        }

        private int FindMainStartIndexSafe(int[] radius, int rMax)
        {
            if (radius == null || radius.Length < 16) return 0;

            // 1) „prawie zero” — to uznajemy za ogon
            int minTailRadiusPx = 2; // <= 2 px — to wyraźnie ogon / pustka
            int tinyByMax = Mathf.RoundToInt(rMax * 0.05f); // 5% od maksimum
            int tiny = Mathf.Max(minTailRadiusPx, tinyByMax);

            // 2) tniemy tylko skrajnie lewy fragment, ograniczamy obszar wyszukiwania
            int maxCheck = Mathf.Min(60, radius.Length / 3);

            int run = 0;
            for (int i = 0; i < maxCheck; i++)
            {
                if (radius[i] <= tiny) run++;
                else break; // gdy tylko wyjdziemy z „prawie zera” — stop
            }

            // 3) jeśli ogon jest rzeczywiście długi — przycinamy; jeśli krótki — nie ruszamy
            int minTailToTrim = 6; // można użyć tailConfirmRun
            if (run >= minTailToTrim) return run;

            return 0;
        }

        private static Color32 MultiplyRgb(Color32 a, Color32 b)
        {
            return new Color32(
                (byte)(a.r * b.r / 255),
                (byte)(a.g * b.g / 255),
                (byte)(a.b * b.b / 255),
                a.a
            );
        }

    }

}
