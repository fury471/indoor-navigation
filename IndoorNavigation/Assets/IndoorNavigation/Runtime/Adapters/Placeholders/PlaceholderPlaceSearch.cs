using System;
using System.Threading;
using System.Threading.Tasks;
using IndoorNavigation.Core.Ports;

namespace IndoorNavigation.Adapters.Placeholders
{
    /// <summary>
    /// Canned answers for two scenarios. "Charger" (any words): a clarifying question first,
    /// then two suggestions. Anything with "print": two printers at once, one of them behind
    /// the studio's steps, to try screen 5b. The developer panel can force the next answer.
    /// Replaced in week 5 by the venue service's search endpoint.
    /// </summary>
    public sealed class PlaceholderPlaceSearch : IPlaceSearch
    {
        private const int ThinkingMilliseconds = 700; // long enough to see screen 3b

        /// <summary>Forces the kind of the next answer once; null means the default flow.</summary>
        public SearchAnswerKind? NextAnswer { get; set; }

        public async Task<SearchAnswer> SearchAsync(SearchRequest request, CancellationToken cancel)
        {
            await Task.Delay(ThinkingMilliseconds, cancel);

            bool printing = request.Text.IndexOf("print", StringComparison.OrdinalIgnoreCase) >= 0;
            SearchAnswerKind kind = NextAnswer
                ?? (printing || request.Answer != null ? SearchAnswerKind.Suggestions : SearchAnswerKind.Question);
            NextAnswer = null;

            if (kind == SearchAnswerKind.Suggestions && printing)
            {
                return SearchAnswer.Found(new[]
                {
                    new Suggestion("printer", "Black and white, in the office on floor 1"),
                    new Suggestion("colour-printer", "Colour, in the studio on floor 2"),
                });
            }

            switch (kind)
            {
                case SearchAnswerKind.Question:
                    return SearchAnswer.Question(
                        "Do you want to borrow a cable, or plug in your own charger?",
                        new[] { "Borrow a cable", "Plug in my own charger", "Not sure, show both" });
                case SearchAnswerKind.Suggestions:
                    return SearchAnswer.Found(new[]
                    {
                        new Suggestion("cable-drawer", "USB-C and Lightning cables to borrow"),
                        new Suggestion("office-1", "Staff at the reception desk lend cables during opening hours"),
                    });
                default:
                    return SearchAnswer.NoMatch("This building's records don't list anything like that. Try other words.");
            }
        }
    }
}
