using IndoorNavigation.Core.Miniature;
using UnityEngine;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// What screen D needs from the 3D renderer, which lives in the App assembly because it
    /// makes meshes, materials and a camera. The renderer draws the miniature into a texture; the
    /// screen shows that texture and turns finger movements into pan, turn, tilt and zoom.
    /// </summary>
    public interface IMiniatureView
    {
        /// <summary>The picture of the miniature, or null before the first <see cref="SetSize"/>.</summary>
        Texture Picture { get; }

        /// <summary>Renders only while the screen is on show, to save battery.</summary>
        void SetActive(bool active);

        /// <summary>The size of the picture in pixels, from the viewport's size on screen.</summary>
        void SetSize(int width, int height);

        /// <summary>Replaces the model drawn. The camera keeps its angle unless the model moved far.</summary>
        void Show(MiniatureModel model);

        /// <summary>
        /// Moves the model with the finger (one-finger drag, as on the 2D map and in map apps): a drag of
        /// <paramref name="pixels"/> in a viewport of <paramref name="viewport"/> pixels.
        /// </summary>
        void Pan(Vector2 pixels, Vector2 viewport);

        /// <summary>Turns the model with a two-finger twist, degrees clockwise on screen.</summary>
        void Rotate(float degrees);

        /// <summary>Tilts with a two-finger vertical swipe: up (negative) looks more from the side, down more from above.</summary>
        void Tilt(float pixels);

        /// <summary>Moves the camera closer (factor below 1) or further (above 1).</summary>
        void Zoom(float factor);

        /// <summary>Back to the starting view: north up as on the 2D map, from 55° above, the route and you in view.</summary>
        void ResetView();

        /// <summary>How far the model is turned from north up, degrees clockwise on screen (0 after <see cref="ResetView"/>).</summary>
        float BearingDegrees { get; }

        /// <summary>Where a point of the model appears, from 0 to 1 across the picture with 0 at the top left; false when behind the camera.</summary>
        bool TryProject(double x, double y, double z, out Vector2 normalised);
    }
}
