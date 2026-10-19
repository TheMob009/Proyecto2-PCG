using UnityEngine;
using UnityEngine.UI;

namespace PCG.Sudoku
{
    /// <summary>Cuadrícula superpuesta con bordes alineados a píxeles de pantalla.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SudokuGridLines : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float thinLineWidth = 1f;
        [SerializeField, Min(0f)] private float blockLineWidth = 4f;

        private readonly Vector3[] corners = new Vector3[4];
        private Rect lastScreenRect;

        protected override void OnEnable()
        {
            // La capa está sobre las casillas, pero deja pasar los clics.
            raycastTarget = false;
            base.OnEnable();
        }

        private void LateUpdate()
        {
            // Un cambio de escala o de posición puede ocurrir sin cambiar el rectángulo local.
            Rect screenRect = GetScreenRect(RenderCamera());
            if (screenRect == lastScreenRect) return;
            lastScreenRect = screenRect;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Camera renderCamera = RenderCamera();
            Rect screenRect = GetScreenRect(renderCamera);
            Rect localRect = rectTransform.rect;
            if (localRect.width <= 0f || localRect.height <= 0f ||
                screenRect.width < 1f || screenRect.height < 1f)
                return;

            float pixelsPerUnit = Mathf.Min(screenRect.width / localRect.width,
                screenRect.height / localRect.height);
            // Dos píxeles también conservan contraste al reducir la vista Game o la página embebida.
            int thinPixels = Mathf.Max(2, Mathf.RoundToInt(thinLineWidth * pixelsPerUnit));
            int blockPixels = Mathf.Max(4, thinPixels + 1,
                Mathf.RoundToInt(blockLineWidth * pixelsPerUnit));

            // Redondear ambos bordes, no solo el centro, evita líneas de medio píxel.
            Rect bounds = Rect.MinMaxRect(Mathf.Round(screenRect.xMin), Mathf.Round(screenRect.yMin),
                Mathf.Round(screenRect.xMax), Mathf.Round(screenRect.yMax));
            for (int division = 0; division <= SudokuBoardView.Size; division++)
            {
                int width = division % 3 == 0 ? blockPixels : thinPixels;
                Vector2 x = PixelLineBounds(bounds.xMin, bounds.xMax, division, width);
                Vector2 y = PixelLineBounds(bounds.yMin, bounds.yMax, division, width);
                AddScreenQuad(mesh, Rect.MinMaxRect(x.x, bounds.yMin, x.y, bounds.yMax), renderCamera);
                AddScreenQuad(mesh, Rect.MinMaxRect(bounds.xMin, y.x, bounds.xMax, y.y), renderCamera);
            }
        }

        internal static Vector2 PixelLineBounds(float minimum, float maximum, int division, int width)
        {
            float thickness = Mathf.Min(width, maximum - minimum);
            if (division == 0) return new Vector2(minimum, minimum + thickness);
            if (division == SudokuBoardView.Size) return new Vector2(maximum - thickness, maximum);

            float center = Mathf.Lerp(minimum, maximum, division / (float)SudokuBoardView.Size);
            float start = Mathf.Clamp(Mathf.Round(center - thickness * 0.5f), minimum, maximum - thickness);
            return new Vector2(start, start + thickness);
        }

        private Camera RenderCamera()
        {
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            return root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        }

        private Rect GetScreenRect(Camera renderCamera)
        {
            rectTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(renderCamera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(renderCamera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private void AddScreenQuad(VertexHelper mesh, Rect bounds, Camera renderCamera)
        {
            int first = mesh.currentVertCount;
            AddScreenVertex(mesh, bounds.xMin, bounds.yMin, renderCamera);
            AddScreenVertex(mesh, bounds.xMin, bounds.yMax, renderCamera);
            AddScreenVertex(mesh, bounds.xMax, bounds.yMax, renderCamera);
            AddScreenVertex(mesh, bounds.xMax, bounds.yMin, renderCamera);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 3, first);
        }

        private void AddScreenVertex(VertexHelper mesh, float x, float y, Camera renderCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                new Vector2(x, y), renderCamera, out Vector2 localPoint);
            mesh.AddVert(new Vector3(localPoint.x, localPoint.y, 0f), color, Vector2.zero);
        }
    }
}
