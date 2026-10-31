using IndoorNavigation.Core.Miniature;
using UnityEngine;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// What screen D needs from the 3D renderer, which lives in the App assembly because it
    /// makes meshes, materials and a camera. The renderer draws the miniature into a texture; the
    /// screen shows that texture and turns finger movements into orbit and zoom.
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

        /// <summary>Turns the camera around the model (yaw) and up or down (pitch), in degrees.</summary>
        void Orbit(float yawDegrees, float pitchDegrees);

        /// <summary>Moves the camera closer (factor below 1) or further (above 1).</summary>
        void Zoom(float factor);

        /// <summary>Back to the starting angle: looking up the plan, turned 25°, down at 50° (35° when floors are stacked), the route and you in view.</summary>
        void ResetView();

        /// <summary>Where a point of the model appears, from 0 to 1 across the picture with 0 at the top left; false when behind the camera.</summary>
        bool TryProject(double x, double y, double z, out Vector2 normalised);
    }
}
