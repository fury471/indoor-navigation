using IndoorNavigation.UI.Developer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IndoorNavigation.App
{
    /// <summary>
    /// Keyboard shortcuts for the developer panel's actions, so the scenario can be driven in
    /// the Editor without clicking. Uses the Input System package, the project's only input
    /// handler. On the iPhone there is no keyboard, so nothing happens.
    /// W panel · D next download fails · F found · L lost · Up raise · Down lower ·
    /// Q question · S suggestions · X no match · N next step · G go to the end
    /// </summary>
    public sealed class DeveloperKeys : MonoBehaviour
    {
        private IDeveloperActions actions;
        private DeveloperPanelPresenter panel;
        private VisualElement uiRoot;

        public void Init(IDeveloperActions developerActions, DeveloperPanelPresenter developerPanel, VisualElement root)
        {
            actions = developerActions;
            panel = developerPanel;
            uiRoot = root;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || actions == null)
            {
                return;
            }

            if (IsTyping())
            {
                return; // the keys are letters, so they must not fire while typing on screen 2
            }

            if (keyboard.wKey.wasPressedThisFrame) panel.Toggle();
            if (keyboard.dKey.wasPressedThisFrame) actions.DownloadFails();
            if (keyboard.fKey.wasPressedThisFrame) actions.PositionFound();
            if (keyboard.lKey.wasPressedThisFrame) actions.PositionLost();
            if (keyboard.upArrowKey.wasPressedThisFrame) actions.Raise();
            if (keyboard.downArrowKey.wasPressedThisFrame) actions.Lower();
            if (keyboard.qKey.wasPressedThisFrame) actions.AskQuestion();
            if (keyboard.sKey.wasPressedThisFrame) actions.Results();
            if (keyboard.xKey.wasPressedThisFrame) actions.NoMatch();
            if (keyboard.nKey.wasPressedThisFrame) actions.NextStep();
            if (keyboard.gKey.wasPressedThisFrame) actions.Arrive();
        }

        private bool IsTyping()
        {
            var focused = uiRoot?.focusController?.focusedElement as VisualElement;
            return focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
        }
    }
}
