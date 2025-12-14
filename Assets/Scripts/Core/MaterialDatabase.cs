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
                    return new Color(0.7f, 0.7f, 0.7f); // szary
                case MaterialType.Aluminium:
                    return new Color(0.85f, 0.85f, 0.9f); // jasny
                case MaterialType.Brass:
                    return new Color(0.9f, 0.8f, 0.2f); // żółty
                default:
                    return Color.white;
            }
        }

        // Na przyszłość: prędkości skrawania, wartości referencyjne itp.
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