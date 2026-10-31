using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using IndoorNavigation.Core.Ports;
using IndoorNavigation.Core.Venue;

namespace IndoorNavigation.Adapters.Placeholders
{
    /// <summary>
    /// Canned answers for two scenarios. "Charger" (any words): a clarifying question first,
    /// then two suggestions. Anything with "print": two printers at once, one of them behind
    /// the studio's steps, to try screen 5b. The developer panel can force the next answer.
    /// For any other building (such as IKDC), it matches the words against the catalogue's names,
    /// types and keywords instead, so its real places can be found ("toilet", "stairs", "VR lab").
    /// Replaced in week 5 by the venue service's search endpoint.
    /// </summary>
    public sealed class PlaceholderPlaceSearch : IPlaceSearch
    {
        private const int ThinkingMilliseconds = 700; // long enough to see screen 3b
        private const int MaxSuggestions = 3;

        private readonly Func<VenuePackage> currentVenue;

        /// <param name="venue">The loaded package, for searching its catalogue; null keeps the canned sample answers.</param>
        public PlaceholderPlaceSearch(Func<VenuePackage> venue = null) => currentVenue = venue;

        /// <summary>Forces the kind of the next answer once; null means the default flow.</summary>
        public SearchAnswerKind? NextAnswer { get; set; }

        public async Task<SearchAnswer> SearchAsync(SearchRequest request, CancellationToken cancel)
        {
            await Task.Delay(ThinkingMilliseconds, cancel);

            VenuePackage venue = currentVenue?.Invoke();
            if (venue != null && venue.FindPlace("cable-drawer") == null)
            {
                SearchAnswerKind? forced = NextAnswer;
                NextAnswer = null;
                return SearchCatalogue(venue, request, forced);
            }

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
    
        /// <summary>Places whose name, type, keywords or description share a word with the request, best first.</summary>
        public static SearchAnswer SearchCatalogue(VenuePackage venue, SearchRequest request, SearchAnswerKind? forced = null)
        {
            string text = (request.Text ?? "") + " " + (request.Answer ?? "");
            var scored = new List<(Place Place, int Score)>();
            foreach (Place place in venue.Places)
            {
                int score = Score(place, text);
                if (score > 0) scored.Add((place, score));
            }

            // Best score first; on a tie, the shorter name is the closer match ("Stairs S1" before
            // "Floor 2, top of stairs S1" for "stairs"), then alphabetical.
            scored.Sort((a, b) =>
                b.Score != a.Score ? b.Score.CompareTo(a.Score)
                : a.Place.Name.Length != b.Place.Name.Length ? a.Place.Name.Length.CompareTo(b.Place.Name.Length)
                : string.CompareOrdinal(a.Place.Name, b.Place.Name));

            if (forced == SearchAnswerKind.NoMatch)
            {
                return SearchAnswer.NoMatch("This building's records don't list anything like that. Try other words.");
            }

            if (forced == SearchAnswerKind.Question && request.Answer == null)
            {
                var options = new List<string>();
                foreach ((Place place, int _) in scored.Count > 0 ? scored : AllPlaces(venue))
                {
                    if (options.Count < MaxSuggestions && !options.Contains(place.Name)) options.Add(place.Name);
                }

                return SearchAnswer.Question("Which one do you mean?", options);
            }

            if (scored.Count == 0 && forced == SearchAnswerKind.Suggestions) scored = AllPlaces(venue);
            if (scored.Count == 0)
            {
                return SearchAnswer.NoMatch("This building's records don't list anything like that. Try other words, such as toilet, lift or stairs.");
            }

            var suggestions = new List<Suggestion>();
            foreach ((Place place, int _) in scored)
            {
                if (suggestions.Count == MaxSuggestions) break;
                suggestions.Add(new Suggestion(place.Id, Reason(venue, place)));
            }

            return SearchAnswer.Found(suggestions);
        }

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "an", "and", "the", "to", "is", "are", "in", "on", "of", "at", "it", "i", "me", "my", "we",
            "where", "want", "go", "get", "need", "find", "please", "can", "how", "there", "here", "for", "with", "show",
        };

        private static List<(Place, int)> AllPlaces(VenuePackage venue)
        {
            var all = new List<(Place, int)>();
            foreach (Place place in venue.Places) all.Add((place, 1));
            return all;
        }

        private static string Reason(VenuePackage venue, Place place)
        {
            string floor = "Floor " + venue.FloorOf(place).ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(place.Description) ? floor : floor + " · " + place.Description;
        }

        /// <summary>Two points for a word of the name, one for the type, a keyword or the description. Plurals count ("toilets").</summary>
        private static int Score(Place place, string text)
        {
            int score = 0;
            foreach (string word in Words(text))
            {
                // Single letters are noise, but a single digit is a floor ("floor 3").
                if ((word.Length < 2 && !char.IsDigit(word[0])) || StopWords.Contains(word)) continue;
                if (Has(place.Name, word)) score += 2;
                else if (Has(place.Type, word) || Has(place.Description, word)) score += 1;
                else
                {
                    foreach (string keyword in place.Keywords ?? Array.Empty<string>())
                    {
                        if (Has(keyword, word))
                        {
                            score += 1;
                            break;
                        }
                    }
                }
            }

            return score;
        }

        private static bool Has(string field, string word)
        {
            if (string.IsNullOrEmpty(field)) return false;
            foreach (string w in Words(field))
            {
                if (w == word || Stem(w) == Stem(word)) return true;
            }

            return false;
        }

        private static string Stem(string w) => w.Length > 3 && w.EndsWith("s", StringComparison.Ordinal) ? w.Substring(0, w.Length - 1) : w;

        private static IEnumerable<string> Words(string text)
        {
            var word = new System.Text.StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(c);
                }
                else if (word.Length > 0)
                {
                    yield return word.ToString();
                    word.Clear();
                }
            }

            if (word.Length > 0) yield return word.ToString();
        }
    }
}
