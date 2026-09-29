using Ganss.Xss;

namespace EduPlatform.Core.Services;

/// <summary>
/// Assainissement du contenu pedagogique redige par les instructeurs.
/// </summary>
/// <remarks>
/// Le contenu des modules est rendu cote Web via <c>MarkupString</c>, donc en HTML brut.
/// Sans filtrage, un instructeur peut injecter du script chez tous ses apprenants
/// (XSS stocke). Le filtrage est applique a deux endroits :
/// a l'ecriture cote API, pour ne rien stocker de dangereux, et au rendu cote Web,
/// pour couvrir les contenus deja presents en base avant ce correctif.
/// </remarks>
public static class LessonHtml
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();
    private static readonly object Gate = new();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "hr", "strong", "b", "em", "i", "u", "s", "mark", "small",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "ul", "ol", "li", "dl", "dt", "dd",
            "blockquote", "pre", "code", "kbd", "samp",
            "a", "img", "figure", "figcaption",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "colgroup", "col",
            "span", "div", "sub", "sup"
        })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[]
        {
            "href", "title", "src", "alt", "width", "height", "colspan", "rowspan",
            "start", "loading", "lang", "dir"
        })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        // Aucun style en ligne ni classe : c'est le vecteur habituel des contournements
        // (url(javascript:), superposition d'un faux formulaire par-dessus la page).
        sanitizer.AllowedCssProperties.Clear();

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        sanitizer.AllowDataAttributes = false;

        // Un lien sortant s'ouvre isole : rel empeche la page cible d'atteindre window.opener.
        sanitizer.PostProcessNode += (_, args) =>
        {
            if (args.Node is not AngleSharp.Dom.IElement element)
                return;

            if (element.TagName.Equals("A", StringComparison.OrdinalIgnoreCase)
                && element.HasAttribute("href"))
            {
                element.SetAttribute("target", "_blank");
                element.SetAttribute("rel", "noopener noreferrer nofollow");
            }

            if (element.TagName.Equals("IMG", StringComparison.OrdinalIgnoreCase))
            {
                element.SetAttribute("loading", "lazy");
            }
        };

        return sanitizer;
    }

    /// <summary>
    /// Retourne le HTML assaini, sans balise ni attribut executable.
    /// Une entree vide ou nulle donne une chaine vide.
    /// </summary>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // HtmlSanitizer n'est pas garanti thread-safe avec un gestionnaire PostProcessNode.
        lock (Gate)
        {
            return Sanitizer.Sanitize(html);
        }
    }
}
