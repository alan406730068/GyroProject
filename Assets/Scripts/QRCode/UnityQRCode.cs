// 來源：https://github.com/codebude/QRCoder.Unity
// MIT License — 直接加入專案，不需要額外 DLL

using UnityEngine;
using QRCoder;

public class UnityQRCode : AbstractQRCode
{
    public UnityQRCode() { }

    public UnityQRCode(QRCodeData data) : base(data) { }

    public Texture2D GetGraphic(int pixelsPerModule)
    {
        return GetGraphic(pixelsPerModule, Color.black, Color.white);
    }

    public Texture2D GetGraphic(int pixelsPerModule, Color darkColor, Color lightColor)
    {
        int size = QrCodeData.ModuleMatrix.Count * pixelsPerModule;
        var tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                int moduleX = x / pixelsPerModule;
                int moduleY = y / pixelsPerModule;
                // QR Code 原點在左上，Texture2D 原點在左下，需要翻轉 Y
                bool isDark = QrCodeData.ModuleMatrix[moduleX][size / pixelsPerModule - 1 - moduleY];
                tex.SetPixel(x, y, isDark ? darkColor : lightColor);
            }
        }

        tex.Apply();
        return tex;
    }
}
