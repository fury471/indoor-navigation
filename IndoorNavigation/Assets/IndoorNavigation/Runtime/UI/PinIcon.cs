using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// The destination pin as a crisp screen icon (blueprint: Destination, #D9480F): a teardrop with a
    /// white centre and a white outline, its tip at the bottom centre of the element.
    /// </summary>
    public sealed class PinIcon : VisualElement
    {
        private static readonly Color Fill = new Color32(0xD9, 0x48, 0x0F, 0xFF);

        public PinIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D p = context.painter2D;
            Rect r = contentRect;
            float radius = Mathf.Min(r.width / 2f, r.height * 0.36f) - 2f;
            var tip = new Vector2(r.width / 2f, r.height - 2f);
            var centre = new Vector2(r.width / 2f, 2f + radius);

            p.fillColor = Fill;
            p.strokeColor = Color.white;
            p.lineWidth = 2.5f;
            p.BeginPath();
            p.MoveTo(tip);
            const int steps = 28;
            for (int i = 0; i <= steps; i++)
            {
                float a = (130f + 280f * i / steps) * Mathf.Deg2Rad; // over the top, screen y down
                p.LineTo(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }

            p.ClosePath();
            p.Fill();
            p.Stroke();

            p.fillColor = Color.white;
            p.BeginPath();
            for (int i = 0; i <= 20; i++)
            {
                float a = i * Mathf.PI * 2f / 20f;
                Vector2 q = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.38f;
                if (i == 0) p.MoveTo(q);
                else p.LineTo(q);
            }

            p.ClosePath();
            p.Fill();
        }
    }
}
