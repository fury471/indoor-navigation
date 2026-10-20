using System.Collections.Generic;
using IndoorNavigation.Core.Session;
using UnityEngine;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// Shows exactly one screen: the one the session's state and view mode select
    /// (decision record 2). Screens never open each other; this is the only place that
    /// switches between them.
    /// </summary>
    public sealed class ScreenHost
    {
        private readonly AppSession session;
        private readonly Dictionary<string, ScreenPresenter> presenters = new Dictionary<string, ScreenPresenter>();
        private ScreenPresenter current;
        private string missingCodeReported;

        public ScreenHost(AppSession session)
        {
            this.session = session;
            session.Changed += Refresh;
        }

        public void Add(string screenCode, ScreenPresenter presenter)
        {
            presenters[screenCode] = presenter;
            ScreenPresenter.SetVisible(presenter.Root, false);
        }

        public void Refresh()
        {
            string code = session.Screen;
            if (!presenters.TryGetValue(code, out ScreenPresenter next))
            {
                if (missingCodeReported != code)
                {
                    Debug.LogWarning($"[IndoorNavigation] Screen {code} is not built yet; keeping the current screen.");
                    missingCodeReported = code;
                }

                return;
            }

            if (next != current)
            {
                if (current != null) ScreenPresenter.SetVisible(current.Root, false);
                ScreenPresenter.SetVisible(next.Root, true);
                current = next;
            }

            current.Render();
        }
    }
}
