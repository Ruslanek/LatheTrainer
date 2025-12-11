using System;
using UnityEngine;

namespace LatheTrainer.Core
{
    [Serializable]
    public struct LatheLimits
    {
        public float XMinMm;
        public float XMaxMm;
        public float ZMinMm;
        public float ZMaxMm;

        public LatheLimits(float xMinMm, float xMaxMm, float zMinMm, float zMaxMm)
        {
            XMinMm = xMinMm;
            XMaxMm = xMaxMm;
            ZMinMm = zMinMm;
            ZMaxMm = zMaxMm;
        }
    }
}