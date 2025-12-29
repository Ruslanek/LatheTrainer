using System;
using System.IO;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class PngSaver : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SaveFileFromUnity(byte[] data, int dataLen, string fileName, string mimeType);
#endif

    /// <summary>
    /// В WebGL: скачивание файла через браузер.
    /// В остальных платформах: сохранение в Application.persistentDataPath (если нужно).
    /// </summary>
    public static void SaveToPersistentFolder(byte[] bytes, string fileName)
    {
        if (bytes == null || bytes.Length == 0)
        {
            Debug.LogWarning("[PNG] Empty data, nothing to save.");
            return;
        }

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "image.png";

        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            fileName += ".png";

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: браузер скачивает файл
        SaveFileFromUnity(bytes, bytes.Length, fileName, "image/png");
        Debug.Log($"[PNG] Browser download requested: {fileName}");

#else
        // Не WebGL: сохраняем в persistentDataPath (или можешь убрать этот блок, если не нужен)
        try
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"[PNG] Saved to persistentDataPath: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PNG] Save failed: {ex}");
        }
#endif
    }
}