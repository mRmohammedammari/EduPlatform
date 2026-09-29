namespace EduPlatform.Web.Services;

/// <summary>
/// Couverture visuelle d'un cours. La vignette televersee par l'instructeur est prioritaire ;
/// a defaut on retombe sur un degrade stable, derive du nom de la categorie.
/// </summary>
/// <remarks>
/// Cette logique etait dupliquee a l'identique dans Home, Courses et Recommendations,
/// et ne couvrait que quatre categories ecrites en dur. Les categories sont libres
/// (saisies par l'instructeur), donc le degrade est choisi par hachage : toute categorie
/// obtient une couleur distincte et deterministe, identique d'une page a l'autre.
/// </remarks>
public static class CourseCoverPalette
{
    private static readonly string[] Gradients =
    {
        "linear-gradient(135deg, #5b5df6 0%, #8b5cf6 100%)",
        "linear-gradient(135deg, #0ea5e9 0%, #2563eb 100%)",
        "linear-gradient(135deg, #10b981 0%, #14b8a6 100%)",
        "linear-gradient(135deg, #f59e0b 0%, #ef4444 100%)",
        "linear-gradient(135deg, #ec4899 0%, #8b5cf6 100%)",
        "linear-gradient(135deg, #0f766e 0%, #0ea5e9 100%)",
        "linear-gradient(135deg, #7c3aed 0%, #2563eb 100%)",
        "linear-gradient(135deg, #b45309 0%, #ec4899 100%)"
    };

    private const string Fallback = "linear-gradient(135deg, #1f2937 0%, #6d28d9 100%)";

    /// <summary>Degrade CSS complet, utilisable dans un attribut <c>style</c>.</summary>
    public static string GradientStyle(string? category) => $"background: {Gradient(category)};";

    public static string Gradient(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Fallback;

        // Hachage FNV-1a sur la categorie normalisee : stable entre les processus,
        // contrairement a string.GetHashCode() qui est randomise par execution.
        var normalized = category.Trim().ToLowerInvariant();
        uint hash = 2166136261;
        foreach (var character in normalized)
        {
            hash ^= character;
            hash *= 16777619;
        }

        return Gradients[hash % Gradients.Length];
    }

    /// <summary>
    /// Vrai si le cours dispose d'une vignette exploitable. Les URL relatives servies par
    /// l'API et les URL absolues sont acceptees ; toute autre valeur retombe sur le degrade.
    /// </summary>
    public static bool HasThumbnail(string? thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            return false;

        var value = thumbnailUrl.Trim();
        return value.StartsWith('/')
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resout la vignette en URL absolue quand l'API la renvoie en chemin relatif
    /// (les fichiers televerses sont servis par l'API, pas par le site web).
    /// </summary>
    public static string ResolveThumbnail(string? thumbnailUrl, string apiBaseUrl)
    {
        var value = (thumbnailUrl ?? string.Empty).Trim();
        if (!value.StartsWith('/'))
            return value;

        return $"{apiBaseUrl.TrimEnd('/')}{value}";
    }
}
