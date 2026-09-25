using System.Text.RegularExpressions;

namespace KrishiLink.BLL.Helpers
{
    /// <summary>
    /// Turns what a farmer types into a PostgreSQL <c>to_tsquery('simple', …)</c> string (DIS-01): each word the user typed
    /// must match (AND), each word is expanded with its Bangla/English synonyms (OR), and every term is a prefix so partial
    /// words still find listings. Only ASCII terms enter the tsquery — whether PostgreSQL tokenizes Bangla depends on the
    /// server locale — so Bangla words contribute through their English synonyms, and callers keep the plain ILIKE on the raw
    /// text as well. Pure: the result is a tsquery built only from [a-z0-9] tokens and operators, never raw user text.
    /// </summary>
    public static class ListingSearch
    {
        /// <summary>Largest page a browse request may ask for (the map view asks for this many pins at once).</summary>
        public const int MaxPageSize = 200;

        /// <summary>Minimum trigram word similarity for the typo fallback ("harvestor" → "Combine Harvester").</summary>
        public const double FuzzyThreshold = 0.3;

        private static readonly Regex WordSplit = new(@"[^\p{L}\p{M}\p{N}]+", RegexOptions.CultureInvariant);
        private static readonly Regex Ascii = new("^[a-z0-9]+$", RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> Words(string? text) =>
            WordSplit.Split((text ?? string.Empty).ToLowerInvariant()).Where(w => w.Length > 0).ToList();

        /// <summary>The tsquery for <paramref name="query"/>, or null when nothing searchable by full text remains.</summary>
        public static string? ToTsQuery(string? query, IReadOnlyList<IReadOnlyList<string>> synonymGroups)
        {
            var words = Words(query).ToList();
            if (words.Count == 0) return null;

            var clauses = new List<string>();
            var consumed = new bool[words.Count];

            // Multi-word synonyms ("cold storage", "পাওয়ার টিলার") are matched as a unit before single words.
            foreach (var (group, phrase) in synonymGroups
                .SelectMany(g => g.Select(term => (Group: g, Phrase: Words(term))))
                .Where(x => x.Phrase.Count > 1)
                .OrderByDescending(x => x.Phrase.Count))
            {
                for (var i = 0; i + phrase.Count <= words.Count; i++)
                {
                    if (Enumerable.Range(0, phrase.Count).Any(k => consumed[i + k] || words[i + k] != phrase[k])) continue;
                    for (var k = 0; k < phrase.Count; k++) consumed[i + k] = true;
                    if (Clause(group) is { } clause) clauses.Add(clause);
                }
            }

            for (var i = 0; i < words.Count; i++)
            {
                if (consumed[i]) continue;
                var alternatives = synonymGroups.Where(g => g.Any(term => term == words[i])).SelectMany(g => g).Append(words[i]);
                if (Clause(alternatives) is { } clause) clauses.Add(clause);
            }

            return clauses.Count == 0 ? null : string.Join(" & ", clauses.Distinct());
        }

        /// <summary>"(cold <-> storage:* | coldstore:*)" from the ASCII terms among <paramref name="terms"/>.</summary>
        private static string? Clause(IEnumerable<string> terms)
        {
            var parts = terms
                .Select(Words)
                .Where(ws => ws.Count > 0 && ws.All(w => Ascii.IsMatch(w)))
                .Select(ws => string.Join(" <-> ", ws.Take(ws.Count - 1).Append(ws[^1] + ":*")))
                .Distinct()
                .ToList();
            return parts.Count switch
            {
                0 => null,
                1 => parts[0],
                _ => "(" + string.Join(" | ", parts) + ")"
            };
        }
    }
}
