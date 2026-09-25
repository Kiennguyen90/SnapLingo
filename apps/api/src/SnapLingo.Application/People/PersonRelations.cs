namespace SnapLingo.Application.People;

/// <summary>One allowed relation: how it is written in a story (English) and in the app
/// (Vietnamese), plus the English words that refer to it.</summary>
public sealed record RelationInfo(string Key, string English, string Vietnamese, IReadOnlyList<string> Words);

/// <summary>
/// The fixed relation list for person labels. The story generator may only use a relation word
/// for someone the learner labeled with it, and the verifier's person rule checks exactly these
/// words, so the list is closed rather than free text.
/// </summary>
public static class PersonRelations
{
    public static readonly IReadOnlyList<RelationInfo> All =
    [
        new("self", "me", "Chính tôi", []),
        new("mother", "my mother", "Mẹ", ["mom", "mother", "mum", "mommy"]),
        new("father", "my father", "Bố", ["dad", "father", "daddy"]),
        new("older_brother", "my older brother", "Anh trai", ["brother", "big brother", "older brother"]),
        new("younger_brother", "my younger brother", "Em trai", ["brother", "little brother", "younger brother"]),
        new("older_sister", "my older sister", "Chị gái", ["sister", "big sister", "older sister"]),
        new("younger_sister", "my younger sister", "Em gái", ["sister", "little sister", "younger sister"]),
        new("grandmother_maternal", "my grandmother", "Bà ngoại", ["grandmother", "grandma", "granny"]),
        new("grandmother_paternal", "my grandmother", "Bà nội", ["grandmother", "grandma", "granny"]),
        new("grandfather_maternal", "my grandfather", "Ông ngoại", ["grandfather", "grandpa"]),
        new("grandfather_paternal", "my grandfather", "Ông nội", ["grandfather", "grandpa"]),
        new("aunt", "my aunt", "Cô / Dì / Bác gái", ["aunt", "auntie"]),
        new("uncle", "my uncle", "Chú / Cậu / Bác trai", ["uncle"]),
        new("cousin", "my cousin", "Anh chị em họ", ["cousin"]),
        new("spouse", "my partner", "Vợ / Chồng", ["wife", "husband", "partner", "spouse"]),
        new("child", "my child", "Con", ["son", "daughter", "child", "kid"]),
        new("partner", "my partner", "Người yêu", ["boyfriend", "girlfriend", "partner"]),
        new("friend", "my friend", "Bạn", ["friend", "friends", "buddy"]),
        new("colleague", "my colleague", "Đồng nghiệp", ["colleague", "coworker", "co-worker", "boss"]),
        new("guest", "a guest", "Khách", ["guest", "visitor"]),
        new("other", "someone I know", "Người quen khác", [])
    ];

    private static readonly Dictionary<string, RelationInfo> ByKey = All.ToDictionary(r => r.Key);

    public static bool IsKnown(string key) => ByKey.ContainsKey(key);

    public static RelationInfo Get(string key) => ByKey.TryGetValue(key, out var r) ? r : ByKey["other"];

    /// <summary>Every relation word the verifier watches for (including "family" words that
    /// name no specific person but still claim who someone is).</summary>
    public static readonly IReadOnlyList<string> AllWords = All
        .SelectMany(r => r.Words)
        .Concat(["parents", "grandparents", "siblings", "family", "relatives", "friends"])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
