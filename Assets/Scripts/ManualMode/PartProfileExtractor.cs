using UnityEngine;

public static class PartProfileExtractor
{
    // alphaThreshold: np. 5..20
    public static PartProfile Extract(Sprite sprite, byte alphaThreshold = 5)
    {
        if (!sprite) return null;

        var tex = sprite.texture;
        var rect = sprite.textureRect; // ważne: textureRect, a nie rect (jeśli używany jest atlas)
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);

        // Odczytujemy piksele dokładnie z obszaru textureRect
        Color32[] pixels = tex.GetPixels32();

        int x0 = Mathf.RoundToInt(rect.x);
        int y0 = Mathf.RoundToInt(rect.y);

        int centerY = h / 2;

        var profile = new PartProfile
        {
            widthPx = w,
            heightPx = h,
            centerY = centerY,
            radiusPx = new int[w],
            xMinMat = w - 1,
            xMaxMat = 0
        };

        bool any = false;

        for (int x = 0; x < w; x++)
        {
            int top = -1;
            int bot = -1;

            // szukamy od góry w dół oraz od dołu w górę
            for (int y = h - 1; y >= 0; y--)
            {
                Color32 c = pixels[(y0 + y) * tex.width + (x0 + x)];
                if (c.a > alphaThreshold) { top = y; break; }
            }

            for (int y = 0; y < h; y++)
            {
                Color32 c = pixels[(y0 + y) * tex.width + (x0 + x)];
                if (c.a > alphaThreshold) { bot = y; break; }
            }

            if (top >= 0 && bot >= 0)
            {
                any = true;
                profile.xMinMat = Mathf.Min(profile.xMinMat, x);
                profile.xMaxMat = Mathf.Max(profile.xMaxMat, x);

                // promień: maksymalne odchylenie od środka
                int rTop = Mathf.Abs(top - centerY);
                int rBot = Mathf.Abs(bot - centerY);
                profile.radiusPx[x] = Mathf.Max(rTop, rBot);
            }
            else
            {
                profile.radiusPx[x] = 0;
            }
        }

        if (!any) return null;

        profile.ComputeMinMax();
        return profile;
    }
}