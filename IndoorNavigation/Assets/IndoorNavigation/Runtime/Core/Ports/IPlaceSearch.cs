using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IndoorNavigation.Core.Ports
{
    /// <summary>What the user asked for, and their answer to a clarifying question if there was one.</summary>
    public sealed class SearchRequest
    {
        public SearchRequest(string venueId, string packageVersion, string text, string answer, bool avoidStairs)
        {
            VenueId = venueId;
            PackageVersion = packageVersion;
            Text = text;
            Answer = answer;
            AvoidStairs = avoidStairs;
        }

        public string VenueId { get; }
        public string PackageVersion { get; }
        public string Text { get; }
        public string Answer { get; }
        public bool AvoidStairs { get; }
    }

    public sealed class Suggestion
    {
        public Suggestion(string placeId, string reason)
        {
            PlaceId = placeId;
            Reason = reason;
        }

        public string PlaceId { get; }

        /// <summary>A one-line reason taken from the recorded facts (Amershi et al. 2019, G11).</summary>
        public string Reason { get; }
    }

    public enum SearchAnswerKind { Question, Suggestions, NoMatch }

    /// <summary>A clarifying question, up to three ranked suggestions, or no match.</summary>
    public sealed class SearchAnswer
    {
        private SearchAnswer(SearchAnswerKind kind, string text, IReadOnlyList<string> options, IReadOnlyList<Suggestion> suggestions)
        {
            Kind = kind;
            Text = text;
            Options = options ?? Array.Empty<string>();
            Suggestions = suggestions ?? Array.Empty<Suggestion>();
        }

        public SearchAnswerKind Kind { get; }

        /// <summary>The question, or the no-match message.</summary>
        public string Text { get; }

        /// <summary>Two to four answer options for a question.</summary>
        public IReadOnlyList<string> Options { get; }

        public IReadOnlyList<Suggestion> Suggestions { get; }

        public static SearchAnswer Question(string question, IReadOnlyList<string> options) =>
            new SearchAnswer(SearchAnswerKind.Question, question, options, null);

        public static SearchAnswer Found(IReadOnlyList<Suggestion> suggestions) =>
            new SearchAnswer(SearchAnswerKind.Suggestions, null, null, suggestions);

        public static SearchAnswer NoMatch(string message) =>
            new SearchAnswer(SearchAnswerKind.NoMatch, message, null, null);
    }

    /// <summary>
    /// Understands a request. Placeholder: canned answers. Real (week 5): the venue service's
    /// search endpoint, which calls the LLM and only returns IDs from the catalogue.
    /// </summary>
    public interface IPlaceSearch
    {
        Task<SearchAnswer> SearchAsync(SearchRequest request, CancellationToken cancel);
    }
}
