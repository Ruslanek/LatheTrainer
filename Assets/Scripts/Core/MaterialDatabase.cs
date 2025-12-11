using UnityEngine;

namespace LatheTrainer.Core
{
    public static class MaterialDatabase
    {
        public static Color GetColor(MaterialType type)
        {
            switch (type)
            {
                case MaterialType.Steel:
                    return new Color(0.7f, 0.7f, 0.7f); // серый
                case MaterialType.Aluminium:
                    return new Color(0.85f, 0.85f, 0.9f); // светлый
                case MaterialType.Brass:
                    return new Color(0.9f, 0.8f, 0.2f); // жёлтый
                default:
                    return Color.white;
            }
        }

        // На будущее: справочные скорости резания и т.п.
        public static float GetRecommendedCuttingSpeed(MaterialType type)
        {
            switch (type)
            {
                case MaterialType.Steel: return 120f;
                case MaterialType.Aluminium: return 250f;
                case MaterialType.Brass: return 180f;
                default: return 100f;
            }
        }
    }
}