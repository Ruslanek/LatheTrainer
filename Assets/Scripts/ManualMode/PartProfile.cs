using System;
using UnityEngine;

[Serializable]
public class PartProfile
{
    public int widthPx;
    public int heightPx;
    public int centerY;

    // Dla każdego x: promień w pikselach (0..height/2)
    public int[] radiusPx;

    // Granice w osi X, gdzie faktycznie występuje materiał
    public int xMinMat;
    public int xMaxMat;

    public int MaxDiameterPx => 2 * MaxRadiusPx;
    public int MinDiameterPx => 2 * MinRadiusPx;

    public int MaxRadiusPx { get; private set; }
    public int MinRadiusPx { get; private set; }

    public void ComputeMinMax()
    {
        MaxRadiusPx = 0;
        MinRadiusPx = int.MaxValue;

        for (int x = xMinMat; x <= xMaxMat; x++)
        {
            int r = radiusPx[x];
            if (r <= 0) continue;

            if (r > MaxRadiusPx) MaxRadiusPx = r;
            if (r < MinRadiusPx) MinRadiusPx = r;
        }

        if (MinRadiusPx == int.MaxValue) MinRadiusPx = 0;
    }
}