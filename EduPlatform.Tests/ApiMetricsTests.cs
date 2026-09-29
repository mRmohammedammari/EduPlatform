using EduPlatform.API.Monitoring;
using Xunit;

namespace EduPlatform.Tests;

public class ApiMetricsTests
{
    [Fact]
    public void ToPrometheus_ReportsCompletedRequestsAndAverageDuration()
    {
        var metrics = new ApiMetrics();

        metrics.RequestStarted();
        metrics.RequestCompleted(200, 10);
        metrics.RequestStarted();
        metrics.RequestCompleted(503, 30);

        var output = metrics.ToPrometheus();

        Assert.Contains("eduplatform_http_requests_total 2", output);
        Assert.Contains("eduplatform_http_errors_total 1", output);
        Assert.Contains("eduplatform_http_active_requests 0", output);
        Assert.Contains("eduplatform_http_request_duration_milliseconds_sum 40", output);
        Assert.Contains("eduplatform_http_request_duration_milliseconds_average 20", output);
    }
}
