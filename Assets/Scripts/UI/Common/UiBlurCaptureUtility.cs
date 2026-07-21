using System.Collections.Generic;
using UnityEngine;

public static class UiBlurCaptureUtility
{
    public static Texture2D CaptureBlurredScreenshot(int outputDownsample, int blurIterations)
    {
        Texture2D source = ScreenCapture.CaptureScreenshotAsTexture();
        if (source == null)
        {
            return null;
        }

        RenderTexture previous = RenderTexture.active;
        List<RenderTexture> temporaryTextures = new List<RenderTexture>();

        try
        {
            int outputDivisor = Mathf.Max(1, outputDownsample);
            int outputWidth = Mathf.Max(32, source.width / outputDivisor);
            int outputHeight = Mathf.Max(32, source.height / outputDivisor);

            RenderTexture current = GetTemporary(source.width, source.height, temporaryTextures);
            Graphics.Blit(source, current);

            int iterations = Mathf.Clamp(blurIterations, 0, 6);
            for (int i = 0; i < iterations; i++)
            {
                int nextWidth = Mathf.Max(16, current.width / 2);
                int nextHeight = Mathf.Max(16, current.height / 2);
                if (nextWidth == current.width && nextHeight == current.height)
                {
                    break;
                }

                RenderTexture next = GetTemporary(nextWidth, nextHeight, temporaryTextures);
                Graphics.Blit(current, next);
                current = next;
            }

            while (current.width < outputWidth || current.height < outputHeight)
            {
                int nextWidth = Mathf.Min(outputWidth, Mathf.Max(current.width + 1, current.width * 2));
                int nextHeight = Mathf.Min(outputHeight, Mathf.Max(current.height + 1, current.height * 2));
                RenderTexture next = GetTemporary(nextWidth, nextHeight, temporaryTextures);
                Graphics.Blit(current, next);
                current = next;
            }

            if (current.width != outputWidth || current.height != outputHeight)
            {
                RenderTexture output = GetTemporary(outputWidth, outputHeight, temporaryTextures);
                Graphics.Blit(current, output);
                current = output;
            }

            RenderTexture.active = current;
            Texture2D blurredTexture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            blurredTexture.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0, false);
            blurredTexture.Apply(false, false);
            blurredTexture.filterMode = FilterMode.Bilinear;
            blurredTexture.wrapMode = TextureWrapMode.Clamp;
            return blurredTexture;
        }
        finally
        {
            RenderTexture.active = previous;
            for (int i = 0; i < temporaryTextures.Count; i++)
            {
                if (temporaryTextures[i] != null)
                {
                    RenderTexture.ReleaseTemporary(temporaryTextures[i]);
                }
            }

            Object.Destroy(source);
        }
    }

    private static RenderTexture GetTemporary(int width, int height, List<RenderTexture> temporaryTextures)
    {
        RenderTexture texture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        temporaryTextures.Add(texture);
        return texture;
    }
}
