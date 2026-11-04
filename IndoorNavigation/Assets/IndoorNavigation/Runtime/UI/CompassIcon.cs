using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// The icon of the Orientation button (blueprint, map controls): a compass needle whose teal half
    /// points to north on screen, so it turns with the map in heading-up, as map apps' compasses do.
    /// </summary>
    public sealed class CompassIcon : VisualElement
    {
        private static readonly Color North = new Color32(0x0F, 0x76, 0x6E, 0xFF);
        private static readonly Color South = new Color32(0xB8, 0xBE, 0xC6, 0xFF);
        private float degrees;

        public CompassIcon()
        {
            pickingMode = PickingMode.Ignore;
            style.width = 24;
            style.height = 24;
            generateVisualContent += Draw;
        }

        /// <summary>Where north is on screen, degrees clockwise from up.</summary>
        public float Degrees
        {
            get => degrees;
            set
            {
                if (Mathf.Abs(value - degrees) < 0.5f) return;
                degrees = value;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D p = context.painter2D;
            Rect r = contentRect;
            Vector2 c = r.center;
            float a = degrees * Mathf.Deg2Rad;
            var up = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            var side = new Vector2(-up.y, up.x);
            float length = Mathf.Min(r.width, r.height) * 0.46f, half = length * 0.32f;

            p.fillColor = North;
            p.BeginPath();
            p.MoveTo(c + up * length);
            p.LineTo(c + side * half);
            p.LineTo(c - side * half);
            p.ClosePath();
            p.Fill();

            p.fillColor = South;
            p.BeginPath();
            p.MoveTo(c - up * length);
            p.LineTo(c + side * half);
            p.LineTo(c - side * half);
            p.ClosePath();
            p.Fill();
        }
    }
}
