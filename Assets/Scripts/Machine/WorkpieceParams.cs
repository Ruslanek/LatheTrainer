using UnityEngine;

namespace LatheTrainer.Machine
{
    /// <summary>
    /// Parametry przedmiotu obrabianego w milimetrach.
    /// </summary>
    [System.Serializable]
    public struct WorkpieceParams
    {
        public MaterialType Material; // stal / aluminium / mosiądz
        public float DiameterMm;      // średnica przedmiotu obrabianego
        public float LengthMm;        // długość przedmiotu obrabianego
    }
}