using EduPlatform.Core.Services;
using Xunit;

namespace EduPlatform.Tests;

public class CourseMediaValidatorTests
{
    [Fact]
    public void ValidateVideo_AcceptsSupportedMp4()
    {
        var error = CourseMediaValidator.ValidateVideo("lesson.mp4", "video/mp4", 1024);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateVideo_RejectsUnsupportedExtension()
    {
        var error = CourseMediaValidator.ValidateVideo("lesson.exe", "application/octet-stream", 1024);

        Assert.Equal("Formats acceptés : mp4, webm, ogg.", error);
    }

    [Fact]
    public void ValidateVideo_RejectsMismatchedContentType()
    {
        var error = CourseMediaValidator.ValidateVideo("lesson.mp4", "video/webm", 1024);

        Assert.Equal("Formats acceptés : mp4, webm, ogg.", error);
    }

    [Fact]
    public void ValidateVideo_RejectsFilesOver100Megabytes()
    {
        var error = CourseMediaValidator.ValidateVideo(
            "lesson.webm", "video/webm", CourseMediaValidator.MaxVideoSize + 1);

        Assert.Equal("La vidéo ne doit pas dépasser 100 Mo.", error);
    }
}
