using UnityEngine;

namespace LatheTrainer.Machine
{
    [CreateAssetMenu(menuName = "Lathe/Material Colors", fileName = "MaterialColorConfig")]
    public class MaterialColorConfig : ScriptableObject
    {
        [Header("Kolory materiałów")]
        public Color steelColor = new Color(0.75f, 0.75f, 0.78f);
        public Color aluminiumColor = new Color(0.85f, 0.87f, 0.92f);
        public Color brassColor = new Color(0.90f, 0.80f, 0.35f);

        public Color GetColor(MaterialType type)
        {
            return type switch
            {
                MaterialType.Steel => steelColor,
                MaterialType.Aluminium => aluminiumColor,
                MaterialType.Brass => brassColor,
                _ => Color.white
            };
        }
    }

   
}