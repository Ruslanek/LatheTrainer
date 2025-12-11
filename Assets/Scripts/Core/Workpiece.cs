namespace LatheTrainer.Core
{
    /// <summary>
    /// Заготовка (исходный цилиндр). 
    /// Позже сюда можно добавить профиль по длине.
    /// </summary>
    public class Workpiece
    {
        public float DiameterMm { get; private set; }
        public float LengthMm { get; private set; }
        public MaterialType Material { get; private set; }

        public Workpiece(float diameterMm, float lengthMm, MaterialType material)
        {
            DiameterMm = diameterMm;
            LengthMm = lengthMm;
            Material = material;
        }

        public void SetSize(float diameterMm, float lengthMm)
        {
            DiameterMm = diameterMm;
            LengthMm = lengthMm;
        }

        public override string ToString()
        {
            return $"{Material}, Ø{DiameterMm} x {LengthMm} mm";
        }
    }
}