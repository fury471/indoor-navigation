using System;
using System.Threading.Tasks;
using IndoorNavigation.Core.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI
{
    /// <summary>
    /// Base class for the presenter of one screen (Model-View-Presenter with a passive view,
    /// Fowler 2006). The UXML file holds the layout and the USS the style; the presenter only
    /// fills in text, shows or hides parts, and forwards taps to the session as requests.
    /// </summary>
    public abstract class ScreenPresenter
    {
        protected ScreenPresenter(VisualElement root, AppSession session)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root), "A screen is missing from AppShell.uxml.");
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>The screen's element in AppShell.uxml.</summary>
        public VisualElement Root { get; }

        protected AppSession Session { get; }

        /// <summary>Copies the session's current data onto the screen. Called after every change.</summary>
        public abstract void Render();

        public static void SetVisible(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Finds a named element, and says clearly which one is missing if the UXML changed.</summary>
        protected T Find<T>(string name) where T : VisualElement =>
            Root.Q<T>(name) ?? throw new InvalidOperationException($"'{name}' is missing in the screen of {GetType().Name}.");

        /// <summary>
        /// Runs a request from a button. Button handlers cannot await, so any error is
        /// written to the Console instead of being lost.
        /// </summary>
        protected static async void RunAsync(Task request)
        {
            try
            {
                await request;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
