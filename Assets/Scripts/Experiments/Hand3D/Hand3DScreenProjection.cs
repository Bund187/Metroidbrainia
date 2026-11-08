using UnityEngine;
using UnityEngine.UI;

namespace Metroidbrainia.Hand3DExperiment
{
    // Maps through the actual UI quad, not through the RenderTexture's pixel dimensions.
    public static class Hand3DScreenProjection
    {
        public static RawImage FindDisplay(Camera camera)
        {
            if (camera == null || camera.targetTexture == null) return null;
            foreach (RawImage image in Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None))
                if (image.gameObject.scene == camera.gameObject.scene && image.texture == camera.targetTexture)
                    return image;
            return null;
        }

        private static Camera UICamera(RawImage display) => display.canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : display.canvas.rootCanvas.worldCamera;

        private static bool CheckDisplay(Camera camera, RawImage display, out string reason)
        {
            reason = null;
            if (display == null)
            {
                if (camera.targetTexture == null) return true;
                reason = "Falta el RawImage que muestra la RenderTexture";
                return false;
            }
            if (!display.isActiveAndEnabled || display.canvas == null || !display.canvas.isActiveAndEnabled)
                reason = "RawImage/Canvas no visible";
            else if (display.canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && display.canvas.rootCanvas.worldCamera == null)
                reason = "Canvas sin cámara UI asignada";
            else if (camera.targetTexture == null || display.texture != camera.targetTexture)
                reason = "RawImage no muestra la RenderTexture de esta cámara";
            else if (display.canvas.rootCanvas.targetDisplay != camera.targetDisplay)
                reason = "RawImage y cámara usan pantallas distintas";
            else if (Mathf.Abs(display.uvRect.width) < 0.00001f || Mathf.Abs(display.uvRect.height) < 0.00001f)
                reason = "UV Rect sin área";
            return reason == null;
        }

        public static bool TryWorldToScreen(Camera camera, RawImage display, Vector3 world, out Vector2 screen, out string reason)
        {
            screen = default;
            reason = null;
            if (camera == null) { reason = "Falta cámara"; return false; }
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= camera.nearClipPlane || viewport.z >= camera.farClipPlane)
            { reason = "Punto detrás de cámara o fuera de sus planos de recorte"; return false; }
            if (!CheckDisplay(camera, display, out reason)) return false;
            if (display == null)
            {
                screen = camera.ViewportToScreenPoint(viewport);
                if (!camera.pixelRect.Contains(screen)) { reason = "Punto fuera de pantalla"; return false; }
                return true;
            }
            // Camera.targetTexture renders the full texture; camera.rect/pixelRect are ignored by Unity.
            Vector2 textureUV = viewport;
            Vector2 uv = new Vector2((textureUV.x - display.uvRect.x) / display.uvRect.width,
                (textureUV.y - display.uvRect.y) / display.uvRect.height);
            Rect rect = display.rectTransform.rect;
            Vector2 local = rect.min + Vector2.Scale(uv, rect.size);
            screen = RectTransformUtility.WorldToScreenPoint(UICamera(display), display.rectTransform.TransformPoint(local));
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f || uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
            { reason = "Punto recortado por cámara o UV Rect"; return false; }
            foreach (RectMask2D mask in display.GetComponentsInParent<RectMask2D>())
                if (mask.isActiveAndEnabled && !RectTransformUtility.RectangleContainsScreenPoint(mask.rectTransform, screen, UICamera(display)))
                { reason = "Punto recortado por RectMask2D"; return false; }
            return true;
        }

        public static bool TryScreenToWorld(Camera camera, RawImage display, Vector2 screen, float depth, out Vector3 world, out string reason)
        {
            world = default;
            reason = null;
            if (camera == null) { reason = "Falta cámara"; return false; }
            if (!CheckDisplay(camera, display, out reason)) return false;
            Vector3 viewport;
            if (display == null) viewport = camera.ScreenToViewportPoint(screen);
            else
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(display.rectTransform, screen, UICamera(display), out Vector2 local))
                { reason = "Pantalla no intersecta el plano del RawImage"; return false; }
                Rect rect = display.rectTransform.rect;
                if (rect.width <= 0f || rect.height <= 0f || !rect.Contains(local))
                { reason = "Pomo fuera del área visible del RawImage"; return false; }
                Vector2 uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
                Vector2 textureUV = display.uvRect.position + Vector2.Scale(uv, display.uvRect.size);
                viewport = new Vector3(textureUV.x, textureUV.y, 0f);
            }
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f || depth <= camera.nearClipPlane || depth >= camera.farClipPlane)
            { reason = "Objetivo fuera del área/profundidad de la cámara"; return false; }
            viewport.z = depth;
            world = camera.ViewportToWorldPoint(viewport);
            return true;
        }
    }
}
