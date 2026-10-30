using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Developer
{
    /// <summary>
    /// Hides the app's screens (#app) while a developer overlay is open, so nothing of the screen
    /// underneath shows through: the recorder needs the bare camera view, and the World maps screen
    /// must not mix with screen 1 or 2. Overlays hold and release it; the screens come back when the
    /// last one is released.
    /// </summary>
    public sealed class AppCover
    {
        private readonly VisualElement screens;
        private int holders;

        public AppCover(VisualElement screens) => this.screens = screens;

        public void Hold()
        {
            holders++;
            if (holders == 1 && screens != null) ScreenPresenter.SetVisible(screens, false);
        }

        public void Release()
        {
            if (holders == 0) return;
            holders--;
            if (holders == 0 && screens != null) ScreenPresenter.SetVisible(screens, true);
        }
    }
}
