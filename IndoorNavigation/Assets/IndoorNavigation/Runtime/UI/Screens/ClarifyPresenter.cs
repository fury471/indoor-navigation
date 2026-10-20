using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Session;
using UnityEngine.UIElements;

namespace IndoorNavigation.UI.Screens
{
    /// <summary>3 · Clarifying question, and 3b · Finding matches while the search works.</summary>
    public sealed class ClarifyPresenter : ScreenPresenter
    {
        private readonly Label request;
        private readonly VisualElement loading;
        private readonly VisualElement questionGroup;
        private readonly Label question;
        private readonly VisualElement options;
        private SearchAnswer renderedAnswer;

        public ClarifyPresenter(VisualElement root, AppSession session) : base(root, session)
        {
            request = Find<Label>("request");
            loading = Find<VisualElement>("loading");
            questionGroup = Find<VisualElement>("question-group");
            question = Find<Label>("question");
            options = Find<VisualElement>("options");
            Find<Button>("cancel").clicked += Session.CancelSearch;
        }

        public override void Render()
        {
            request.text = Session.RequestText ?? "";
            bool asking = !Session.Searching && Session.Answer != null && Session.Answer.Kind == SearchAnswerKind.Question;
            SetVisible(loading, Session.Searching);
            SetVisible(questionGroup, asking);
            if (!asking || renderedAnswer == Session.Answer)
            {
                return;
            }

            renderedAnswer = Session.Answer;
            question.text = Session.Answer.Text;
            options.Clear();
            foreach (string option in Session.Answer.Options)
            {
                var button = new Button(() => RunAsync(Session.AnswerQuestionAsync(option))) { text = option };
                button.AddToClassList("button");
                options.Add(button);
            }
        }
    }
}
