namespace EduPlatform.Core.Services;

public static class CourseMediaValidator
{
    public const long MaxVideoSize = 100_000_000;

    private static readonly IReadOnlyDictionary<string, string> VideoContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".ogg"] = "video/ogg"
        };

    public static string? ValidateVideo(string fileName, string contentType, long length)
    {
        if (length <= 0)
            return "Sélectionnez une vidéo.";
        if (length > MaxVideoSize)
            return "La vidéo ne doit pas dépasser 100 Mo.";

        var extension = Path.GetExtension(fileName);
        if (!VideoContentTypes.TryGetValue(extension, out var expectedContentType)
            || !string.Equals(expectedContentType, contentType, StringComparison.OrdinalIgnoreCase))
            return "Formats acceptés : mp4, webm, ogg.";

        return null;
    }
}
