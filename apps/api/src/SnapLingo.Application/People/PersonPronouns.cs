namespace SnapLingo.Application.People;

/// <summary>
/// How a labeled person is referred to. The learner picks the gender (never inferred from a photo);
/// for most family relations it is implied by the relation itself. When it is neither, the story
/// must use the person's name or "they" instead of guessing "he"/"she".
/// </summary>
public static class PersonPronouns
{
    public const string Female = "female";
    public const string Male = "male";

    private static readonly HashSet<string> FemaleRelations =
        ["mother", "older_sister", "younger_sister", "grandmother_maternal", "grandmother_paternal", "aunt"];

    private static readonly HashSet<string> MaleRelations =
        ["father", "older_brother", "younger_brother", "grandfather_maternal", "grandfather_paternal", "uncle"];

    /// <summary>"female", "male" or null when neither the learner nor the relation says.</summary>
    public static string? GenderOf(string relation, string? gender)
    {
        if (gender is Female or Male)
        {
            return gender;
        }

        return FemaleRelations.Contains(relation) ? Female
            : MaleRelations.Contains(relation) ? Male
            : null;
    }

    /// <summary>English subject/object pronouns the story may use, e.g. "she/her".</summary>
    public static string English(string relation, string? gender) => GenderOf(relation, gender) switch
    {
        Female => "she/her",
        Male => "he/him",
        _ => "they/them"
    };

    /// <summary>
    /// Vietnamese way to refer to this person after the first mention: Vietnamese picks the word by
    /// relation and age ("mẹ", "anh ấy", "bà"), not just by gender, so a flat "cô ấy" is often wrong.
    /// </summary>
    public static string Vietnamese(string relation, string? gender)
    {
        var g = GenderOf(relation, gender);
        return relation switch
        {
            "self" => "tôi",
            "mother" => "mẹ",
            "father" => "bố",
            "older_brother" => "anh ấy",
            "younger_brother" => "em ấy",
            "older_sister" => "chị ấy",
            "younger_sister" => "em ấy",
            "grandmother_maternal" or "grandmother_paternal" => "bà",
            "grandfather_maternal" or "grandfather_paternal" => "ông",
            "aunt" => "cô ấy",
            "uncle" => "chú ấy",
            "spouse" => g == Female ? "vợ tôi" : g == Male ? "chồng tôi" : "người ấy",
            "child" => g == Female ? "con bé" : g == Male ? "thằng bé" : "con",
            _ => g == Female ? "cô ấy" : g == Male ? "anh ấy" : "bạn ấy"
        };
    }

    /// <summary>True when the gendered English pronoun matches someone the learner labeled.</summary>
    public static bool Allows(string pronoun, IEnumerable<(string Relation, string? Gender)> people)
    {
        var wanted = pronoun.ToLowerInvariant() switch
        {
            "he" or "him" or "his" or "himself" => Male,
            "she" or "her" or "hers" or "herself" => Female,
            _ => null
        };
        return wanted is not null && people.Any(p => GenderOf(p.Relation, p.Gender) == wanted);
    }
}
