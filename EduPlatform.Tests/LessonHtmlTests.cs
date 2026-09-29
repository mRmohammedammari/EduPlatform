using EduPlatform.Core.Services;
using Xunit;

namespace EduPlatform.Tests;

/// <summary>
/// Le contenu des modules est rendu en HTML brut cote Web (MarkupString).
/// Ces tests verrouillent la frontiere : ce qui passe, ce qui est retire.
/// </summary>
public class LessonHtmlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_EntreeVide_RetourneChaineVide(string? input)
    {
        Assert.Equal(string.Empty, LessonHtml.Sanitize(input));
    }

    [Fact]
    public void Sanitize_ConserveLeBalisagePedagogique()
    {
        const string input = """
            <h2>Titre</h2>
            <p>Un <strong>point important</strong> et du <code>code</code>.</p>
            <ul><li>Premier</li><li>Second</li></ul>
            <table><thead><tr><th>Col</th></tr></thead><tbody><tr><td>Valeur</td></tr></tbody></table>
            <pre><code>var x = 1;</code></pre>
            """;

        var result = LessonHtml.Sanitize(input);

        Assert.Contains("<h2>Titre</h2>", result);
        Assert.Contains("<strong>point important</strong>", result);
        Assert.Contains("<li>Premier</li>", result);
        Assert.Contains("<td>Valeur</td>", result);
        Assert.Contains("var x = 1;", result);
    }

    [Fact]
    public void Sanitize_RetireLesBalisesScript()
    {
        var result = LessonHtml.Sanitize("<p>Avant</p><script>alert('xss')</script><p>Apres</p>");

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avant", result);
        Assert.Contains("Apres", result);
    }

    [Theory]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\" />", "onerror")]
    [InlineData("<div onclick=\"steal()\">Cliquez</div>", "onclick")]
    [InlineData("<p onmouseover=\"track()\">Survolez</p>", "onmouseover")]
    [InlineData("<body onload=\"go()\">x</body>", "onload")]
    public void Sanitize_RetireLesGestionnairesEvenement(string input, string handler)
    {
        var result = LessonHtml.Sanitize(input);

        Assert.DoesNotContain(handler, result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<a href=\"javascript:alert(1)\">lien</a>")]
    [InlineData("<a href=\"JaVaScRiPt:alert(1)\">lien</a>")]
    [InlineData("<a href=\"data:text/html;base64,PHNjcmlwdD4=\">lien</a>")]
    [InlineData("<a href=\"vbscript:msgbox(1)\">lien</a>")]
    public void Sanitize_RetireLesSchemasDangereux(string input)
    {
        var result = LessonHtml.Sanitize(input);

        Assert.DoesNotContain("javascript", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_RetireLesIframesEtObjets()
    {
        const string input = """
            <iframe src="https://evil.example/x"></iframe>
            <object data="x.swf"></object>
            <embed src="x.swf" />
            <form action="https://evil.example"><input name="password" /></form>
            """;

        var result = LessonHtml.Sanitize(input);

        Assert.DoesNotContain("<iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<embed", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_RetireLesStylesEnLigne()
    {
        // Un style en ligne permet de recouvrir la page d'un faux formulaire.
        var result = LessonHtml.Sanitize(
            "<p style=\"position:fixed;inset:0;z-index:9999;background:#fff\">Faux ecran</p>");

        Assert.DoesNotContain("position", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("9999", result);
        Assert.Contains("Faux ecran", result);
    }

    [Fact]
    public void Sanitize_RetireLesAttributsData()
    {
        var result = LessonHtml.Sanitize("<p data-payload=\"x\">Texte</p>");

        Assert.DoesNotContain("data-payload", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Texte", result);
    }

    [Fact]
    public void Sanitize_IsoleLesLiensSortants()
    {
        var result = LessonHtml.Sanitize("<a href=\"https://exemple.test/doc\">Documentation</a>");

        Assert.Contains("https://exemple.test/doc", result);
        Assert.Contains("target=\"_blank\"", result);
        Assert.Contains("noopener", result);
        Assert.Contains("noreferrer", result);
    }

    [Fact]
    public void Sanitize_ConserveLesImagesHttpEtForceLeChargementDiffere()
    {
        var result = LessonHtml.Sanitize(
            "<img src=\"https://exemple.test/schema.png\" alt=\"Schema\" width=\"600\" />");

        Assert.Contains("https://exemple.test/schema.png", result);
        Assert.Contains("alt=\"Schema\"", result);
        Assert.Contains("loading=\"lazy\"", result);
    }

    [Fact]
    public void Sanitize_ConserveLesLiensMailto()
    {
        var result = LessonHtml.Sanitize("<a href=\"mailto:prof@exemple.test\">Ecrire</a>");

        Assert.Contains("mailto:prof@exemple.test", result);
    }

    [Fact]
    public void Sanitize_EstIdempotent()
    {
        const string input = "<p>Texte <script>alert(1)</script><a href=\"https://exemple.test\">lien</a></p>";

        var once = LessonHtml.Sanitize(input);
        var twice = LessonHtml.Sanitize(once);

        Assert.Equal(once, twice);
    }
}
