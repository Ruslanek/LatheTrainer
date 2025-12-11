using UnityEngine;

namespace LatheTrainer.Core
{
    public enum ToolType
    {
        ExternalTurning,   // продольное точение
        Facing,            // подрезание торца
        Parting,           // отрезной
        Grooving           // канавочный, опционально
    }

    public class Tool
    {
        public string Name { get; private set; }
        public ToolType Type { get; private set; }

        /// <summary>Вылет державки из резцедержателя (мм).</summary>
        public float OverhangMm { get; private set; }

        /// <summary>Радиус вершины пластины (мм).</summary>
        public float NoseRadiusMm { get; private set; }

        /// <summary>Ширина режущей кромки (мм). Для отрезного/канавочного.</summary>
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