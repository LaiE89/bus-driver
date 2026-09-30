using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusDriver.Tests.PlayMode.Flow {
    // Writes a camera's view, with an overlay canvas drawn into it, to Logs/smoke/<name>.png for a
    // person to look at (§4.19). Overlay canvases never reach a camera's target, so the canvas is
    // switched to camera space for the one render.
    static class CaptureUtil {
        public static string Folder { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "smoke")); } }

        public static void CaptureWithCanvas(string fileName, Camera camera, Canvas canvas) {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || camera == null || canvas == null) {
                return;
            }
            RenderMode mode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float plane = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.05f, 0.5f);
            Canvas.ForceUpdateCanvases();

            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) {
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(Path.Combine(Folder, fileName + ".png"), image.EncodeToPNG());
                Object.Destroy(image);
            }
            rt.Release();
            canvas.renderMode = mode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = plane;
        }
    }
}
