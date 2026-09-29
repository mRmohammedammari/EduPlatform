using EduPlatform.Data.Cassandra.Repositories;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Globalization;
using System.Text;

namespace EduPlatform.API.Controllers;

[ApiController]
[Route("api/certificates")]
[Authorize]
public class CertificatesController : ControllerBase
{
    private readonly EduDbContext _db;
    private readonly TestResultRepository _testResults;
    private readonly IConfiguration _configuration;

    public CertificatesController(
        EduDbContext db,
        TestResultRepository testResults,
        IConfiguration configuration)
    {
        _db = db;
        _testResults = testResults;
        _configuration = configuration;
    }

    [HttpGet("{courseId:guid}")]
    public async Task<IActionResult> GetCertificate(Guid courseId)
    {
        var userId = GetCurrentUserId();
        var certificate = await LoadCertificateAsync(userId, courseId);
        if (certificate.Error is not null)
            return certificate.Error;

        return Ok(ToResponse(certificate.Data!));
    }

    [HttpGet("{courseId:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid courseId)
    {
        var userId = GetCurrentUserId();
        var certificate = await LoadCertificateAsync(userId, courseId);
        if (certificate.Error is not null)
            return certificate.Error;

        var data = certificate.Data!;
        var pdf = SimplePdfDocument.Create(
            "EduPlatform - Certificat de reussite",
            $"Certificat de reussite\n\nDecerne a : {data.StudentName}\nCours : {data.CourseTitle}\nScore : {data.Score.ToString(CultureInfo.InvariantCulture)} / {data.MaxScore.ToString(CultureInfo.InvariantCulture)}\nDelivre le : {data.IssuedAt.ToLocalTime():dd/MM/yyyy}\nIdentifiant : {data.CertificateId}\n\nVerification : {data.VerificationUrl}");

        return File(pdf, "application/pdf", $"{data.CertificateId}.pdf");
    }

    [AllowAnonymous]
    [HttpGet("verify/{certificateId}")]
    public async Task<IActionResult> Verify(string certificateId)
    {
        var parts = certificateId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !Guid.TryParse(parts[1], out var userId) ||
            !Guid.TryParse(parts[2], out var courseId))
        {
            return NotFound(new { valid = false, message = "Certificat introuvable." });
        }

        var certificate = await LoadCertificateAsync(userId, courseId);
        if (certificate.Error is not null)
            return NotFound(new { valid = false, message = "Certificat introuvable." });

        var data = certificate.Data!;
        return Ok(new
        {
            valid = true,
            certificateId = data.CertificateId,
            studentName = data.StudentName,
            courseTitle = data.CourseTitle,
            issuedAt = data.IssuedAt,
            score = data.Score,
            maxScore = data.MaxScore
        });
    }

    [AllowAnonymous]
    [HttpGet("verify/{certificateId}/qr")]
    public async Task<IActionResult> VerificationQrCode(string certificateId)
    {
        var parts = certificateId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !Guid.TryParse(parts[1], out var userId) ||
            !Guid.TryParse(parts[2], out var courseId))
        {
            return NotFound();
        }

        var certificate = await LoadCertificateAsync(userId, courseId);
        if (certificate.Error is not null)
            return NotFound();

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(certificate.Data!.VerificationUrl, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(qrData).GetGraphic(5);
        return Content(svg, "image/svg+xml; charset=utf-8");
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirst(
        System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    private async Task<(CertificateData? Data, IActionResult? Error)> LoadCertificateAsync(
        Guid userId,
        Guid courseId)
    {
        var enrolled = await _db.Enrollments.AnyAsync(enrollment =>
            enrollment.UserId == userId && enrollment.CourseId == courseId);
        if (!enrolled)
            return (null, Forbid());

        var course = await _db.Courses.FirstOrDefaultAsync(item =>
            item.Id == courseId && item.IsPublished && !item.IsArchived);
        if (course == null)
            return (null, NotFound());

        var result = await _testResults.GetBestPassedResultAsync(userId, courseId);
        if (result == null)
            return (null, Conflict(new { message = "Réussissez le test pour obtenir le certificat." }));

        var student = await _db.Users.FirstOrDefaultAsync(user => user.Id == userId);
        var certificateId = $"EDU-{userId:N}-{courseId:N}";
        var baseUrl = _configuration["PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        return (new CertificateData(
            certificateId,
            student?.FirstName ?? "Étudiant",
            course.Title,
            result.TakenAt,
            result.Score,
            result.MaxScore,
            $"{baseUrl.TrimEnd('/')}/api/certificates/verify/{certificateId}"), null);
    }

    private static object ToResponse(CertificateData data) => new
    {
        certificateId = data.CertificateId,
        studentName = data.StudentName,
        courseTitle = data.CourseTitle,
        issuedAt = data.IssuedAt,
        score = data.Score,
        maxScore = data.MaxScore,
        verificationUrl = data.VerificationUrl
    };

    private sealed record CertificateData(
        string CertificateId,
        string StudentName,
        string CourseTitle,
        DateTime IssuedAt,
        decimal Score,
        decimal MaxScore,
        string VerificationUrl);

    private static class SimplePdfDocument
    {
        public static byte[] Create(string title, string body)
        {
            var lines = body.Split('\n');
            var content = new StringBuilder($"BT /F1 18 Tf 72 760 Td ({Escape(title)}) Tj 0 -32 Td /F1 11 Tf ");
            foreach (var line in lines)
            {
                content.Append('(').Append(Escape(line)).Append(") Tj 0 -18 Td ");
            }
            content.Append("ET");

            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}\nendstream"
            };

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true);
            writer.WriteLine("%PDF-1.4");
            var offsets = new List<long> { 0 };
            for (var index = 0; index < objects.Length; index++)
            {
                writer.Flush();
                offsets.Add(stream.Position);
                writer.WriteLine($"{index + 1} 0 obj");
                writer.WriteLine(objects[index]);
                writer.WriteLine("endobj");
            }
            writer.Flush();
            var xref = stream.Position;
            writer.WriteLine($"xref\n0 {objects.Length + 1}\n0000000000 65535 f ");
            foreach (var offset in offsets.Skip(1))
                writer.WriteLine($"{offset:0000000000} 00000 n ");
            writer.WriteLine($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
            writer.Flush();
            return stream.ToArray();
        }

        private static string Escape(string value) => value
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", string.Empty)
            .Replace("\n", " ");
    }
}
