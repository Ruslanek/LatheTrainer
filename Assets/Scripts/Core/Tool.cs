using UnityEngine;

namespace LatheTrainer.Core
{
    public enum ToolType
    {
        ExternalTurning,   // toczenie wzdłużne
        Facing,            // planowanie czoła
        Parting,           // toczenie odcinające
        Grooving           // toczenie rowków (opcjonalnie)
    }

    public class Tool
    {
        public string Name { get; private set; }
        public ToolType Type { get; private set; }

        /// <summary>Wysięg oprawki z imaka narzędziowego (mm).</summary>
        public float OverhangMm { get; private set; }

        /// <summary>Promień wierzchołka płytki skrawającej (mm).</summary>
        public float NoseRadiusMm { get; private set; }

        /// <summary>Szerokość krawędzi skrawającej (mm). Dotyczy toczenia odcinającego i rowkowania.</summary>
        public float WidthMm { get; private set; }

        public Tool(string name, ToolType type,
                    float overhangMm, float noseRadiusMm, float widthMm)
        {
            Name = name;
            Type = type;
            OverhangMm = overhangMm;
            NoseRadiusMm = noseRadiusMm;
            WidthMm = widthMm;
        }

        public override string ToString()
        {
            return $"{Name} ({Type}), L={OverhangMm} R={NoseRadiusMm} W={WidthMm}";
        }
    }
}