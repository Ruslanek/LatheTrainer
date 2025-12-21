using UnityEngine;

namespace LatheTrainer.Machine
{
    public class ToolCutProfile : MonoBehaviour
    {
        public enum ContactShape
        {
            AABB,   // axis-aligned bounds
            OBB,    // oriented (по BoxCollider2D/Transform)
            Polygon,
            Radius  // okrągły wierzchołek (promień)
        }

        [Header("Contact")]
        public ContactShape shape = ContactShape.OBB;

        [Tooltip("Używane tylko gdy shape = Radius. Promień wierzchołka w mm.")]
        public float tipRadiusMm = 0.8f;

        [Tooltip("Jeśli true — skrawanie po dolnej krawędzi (narzędzie od góry). Jeśli false — po górnej (narzędzie od dołu).")]
        public bool toolAboveWorkpiece = true;

        [Header("Look")]
        public Color machinedFillColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        public int machinedFillThicknessPx = 4;  // ile pikseli do wewnątrz malować jako „obrobione”
    }
}