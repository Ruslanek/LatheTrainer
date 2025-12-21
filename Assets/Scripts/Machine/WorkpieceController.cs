using UnityEngine;

namespace LatheTrainer.Machine
{
    public class WorkpieceController : MonoBehaviour
    {
        [Header("Refs")]
        public Transform chuckFace;              // punkt / płaszczyzna czoła uchwytu (referencja WORLD)
        public Transform chuckStatic;            // sam uchwyt (Transform)
        public SpriteRenderer chuckRenderer;     // SpriteRenderer uchwytu (do obliczeń bounds)
        public SpriteRenderer workpieceRenderer; // SpriteRenderer detalu

        [Header("Material colors")]
        public MaterialColorConfig materialColors;

        [Header("Units (mm -> unity units)")]
        public float unitsPerMm = 0.01f;

        [Header("Ratios / gaps")]
        public float chuckToWorkpieceRatio = 1.6f;
        public float gapMm = 8f;

        [Header("Direction")]
        [Tooltip("+1 jeśli detal znajduje się na prawo od czoła, -1 jeśli po lewej")]
        public float xDir = 1f;

        [Header("Base sizes (units at scale=1)")]
        public float baseChuckDiameterUnits = 1f;
        public float baseWorkpieceDiameterUnits = 1f;
        public float baseWorkpieceLengthUnits = 1f;

        private Vector3 _chuckBaseScale;
        private Vector3 _wpBaseScale;

        [SerializeField] private ChuckSpindleVisual chuckVisual;

        [Header("Domyślna detal przy starcie")]
        [SerializeField] private MaterialType defaultMaterial = MaterialType.Steel;
        [SerializeField] private float defaultDiameterMm = 100f;
        [SerializeField] private float defaultLengthMm = 150f;

        [SerializeField] private WorkpieceMachiningTexture machining;

        private void Awake()
        {

            if (chuckStatic) _chuckBaseScale = chuckStatic.localScale;
            if (workpieceRenderer) _wpBaseScale = workpieceRenderer.transform.localScale;
        }

        private void Start()
        {
            ApplyFromMm(defaultMaterial, defaultDiameterMm, defaultLengthMm);
        }

        // ====== wejście w milimetrach ======
        public void ApplyFromMm(MaterialType material, float diameterMm, float lengthMm)
        {
            float workpieceDiameterU = diameterMm * unitsPerMm;
            float workpieceLengthU = lengthMm * unitsPerMm;

            float chuckDiameterU = (diameterMm * chuckToWorkpieceRatio) * unitsPerMm;
            float gapU = gapMm * unitsPerMm;

            ApplyUnits(material, workpieceDiameterU, workpieceLengthU, chuckDiameterU, gapU);
        }

        // ======  dane wejściowe w jednostkach (units) ======
        public void ApplyUnits(MaterialType material, float workpieceDiameterU, float workpieceLengthU,
                               float chuckDiameterU, float gapU)
        {



            if (chuckFace == null || chuckStatic == null || workpieceRenderer == null)
            {
                Debug.LogError("WorkpieceController: nie przypisano chuckFace / chuckStatic / workpieceRenderer");
                return;
            }

            // 1) skalowanie uchwytu (TYLKO oś Y)
            float chuckK = chuckDiameterU / Mathf.Max(0.0001f, baseChuckDiameterUnits);
            chuckStatic.localScale = new Vector3(_chuckBaseScale.x, _chuckBaseScale.y * chuckK, _chuckBaseScale.z);

            // 2) grubość uchwytu w osi X (jednostki WORLD)
            float chuckThicknessU = 1f;
            if (chuckRenderer != null)
                chuckThicknessU = chuckRenderer.bounds.size.x;
            else
                chuckThicknessU = Mathf.Abs(_chuckBaseScale.x); // wariant zapasowy, jeśli renderer nie jest przypisany
            CaptureWorkpieceBaseUnits();


            // 3) skalowanie detalu (X = długość, Y = średnica)
            float wpKDia = workpieceDiameterU / Mathf.Max(0.0001f, baseWorkpieceDiameterUnits);
            float wpKLen = workpieceLengthU / Mathf.Max(0.0001f, baseWorkpieceLengthUnits);

            // Debug.Log($"_wpBaseScale={_wpBaseScale} baseDia={baseWorkpieceDiameterUnits} baseLen={baseWorkpieceLengthUnits}");
            // Debug.Log($"wpKDia={wpKDia:F4} wpKLen={wpKLen:F4}");


            workpieceRenderer.transform.localScale =
                new Vector3(_wpBaseScale.x * wpKLen, _wpBaseScale.y * wpKDia, _wpBaseScale.z);

            // kolor
            if (materialColors) workpieceRenderer.color = materialColors.GetColor(material);




            // 4) pozycje (WORLD)
            float faceX = chuckFace.position.x;

            // środek uchwytu = czoło + połowa grubości
            float chuckCenterX = faceX + xDir * (chuckThicknessU * 0.5f);
            var chuckPos = chuckStatic.position;
            chuckPos.x = chuckCenterX;
            chuckStatic.position = chuckPos;


            // po przeskalowaniu workpieceRenderer.transform.localScale

            float chuckFaceX = chuckCenterX + xDir * (chuckThicknessU * 0.5f);

            // rzeczywista połowa długości detalu w WORLD (po skali)
            float halfLenWorld = workpieceRenderer.bounds.size.x * 0.5f;

            // środek detalu = czoło uchwytu + szczelina + połowa rzeczywistej długości (wszystko w kierunku xDir)
            float wpCenterX = chuckFaceX + xDir * (gapU + halfLenWorld);

            float leftFaceOfWorkpiece = workpieceRenderer.bounds.min.x;
            float gapCheck = (leftFaceOfWorkpiece - chuckFaceX) * xDir; // normalizujemy względem kierunku

            // Debug.Log($"gap target={gapU:F4}, gap actual={gapCheck:F4}, halfLenWorld={halfLenWorld:F4}");

            var wpPos = workpieceRenderer.transform.position;
            wpPos.x = wpCenterX;
            workpieceRenderer.transform.position = wpPos;


            /* //  float faceX = chuckFace.position.x;

             // jeśli detal znajduje się po prawej stronie
             float chuckFaceX = chuckCenterX + xDir * (chuckThicknessU * 0.5f);
             // float wpCenterX = chuckFaceX + xDir * (gapU + workpieceLengthU * 0.5f);



             Debug.Log($"gapU(world)={gapU:F4}, workpieceLengthU={workpieceLengthU:F4}");
             Debug.Log($"workpieceRenderer.lossyScale={workpieceRenderer.transform.lossyScale}");
             Debug.Log($"workpiece bounds.size.x(world)={workpieceRenderer.bounds.size.x:F4}");

             //float wpCenterX = chuckFaceX +gapU+ (xDir * (workpieceLengthU * 0.5f));
             float wpCenterX = chuckFaceX + (xDir * gapU) + (xDir * (workpieceLengthU * 0.5f));


             var wpPos = workpieceRenderer.transform.position;
             wpPos.x = wpCenterX;
             workpieceRenderer.transform.position = wpPos;*/

            //Debug.Log($"APPLY: faceX={faceX:F3} chuckThickness={chuckThicknessU:F3} chuckCenterX={chuckCenterX:F3}  gapU={gapU:F3}");

            //Debug.Log($"[WP] ApplyUnits: diameterMm? (raw not here), workpieceDiameterU={workpieceDiameterU:F4}");
            // Debug.Log($"[WP] workpieceSprite.bounds.size.y (world)={workpieceRenderer.bounds.size.y:F4}");
            // Debug.Log($"[WP] baseWorkpieceDiameterUnits={baseWorkpieceDiameterUnits:F4}, unitsPerMm={unitsPerMm:F6}");

            // 5)  uruchomienie animacji zaciskania szczęk dla wybranego detalu
            if (chuckVisual != null)
            {
                float realDiameterWorld = workpieceRenderer.bounds.size.y;

                //Debug.Log($"[WP] Clamp start, real diameter world = {realDiameterWorld:F4}");

                chuckVisual.SelectWorkpieceByDiameter(realDiameterWorld);
            }
            else
            {
                Debug.LogWarning("WorkpieceController: chuckVisual nie jest przypisany");
            }

            if (machining != null)
                machining.TryInitRuntimeTexture();
        }

        public void ApplyParams(WorkpieceParams p)
        {


            // stara ścieżka: wejście w milimetrach
            ApplyFromMm(p.Material, p.DiameterMm, p.LengthMm);
        }

        private void CaptureWorkpieceBaseUnits()
        {
            if (!workpieceRenderer) return;

            // WAŻNE: mierzymy bounds w aktualnym stanie „bazowym” (przed ApplyUnits)
            var b = workpieceRenderer.bounds;

            baseWorkpieceLengthUnits = b.size.x; // X = długość
            baseWorkpieceDiameterUnits = b.size.y; // Y = średnica

            _wpBaseScale = workpieceRenderer.transform.localScale;

            // Debug.Log($"[Base WP] baseLen={baseWorkpieceLengthUnits:F4}, baseDia={baseWorkpieceDiameterUnits:F4}, baseScale={_wpBaseScale}");
        }
    }
}