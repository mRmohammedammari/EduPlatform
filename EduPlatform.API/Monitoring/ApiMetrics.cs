using System.Text;
using System.Threading;

namespace EduPlatform.API.Monitoring;

public sealed class ApiMetrics
{
    private long _requests;
    private long _errors;
    private long _activeRequests;
    private long _durationMilliseconds;

    public void RequestStarted()
    {
        Interlocked.Increment(ref _activeRequests);
    }

    public void RequestCompleted(int statusCode, long elapsedMilliseconds)
    {
        Interlocked.Increment(ref _requests);
        if (statusCode >= 500)
        {
            Interlocked.Increment(ref _errors);
        }

        Interlocked.Add(ref _durationMilliseconds, elapsedMilliseconds);
        Interlocked.Decrement(ref _activeRequests);
    }

    public string ToPrometheus()
    {
        var requests = Interlocked.Read(ref _requests);
        var errors = Interlocked.Read(ref _errors);
        var activeRequests = Interlocked.Read(ref _activeRequests);
        var durationMilliseconds = Interlocked.Read(ref _durationMilliseconds);
        var averageDuration = requests == 0 ? 0 : (double)durationMilliseconds / requests;

        var output = new StringBuilder()
            .AppendLine("# HELP eduplatform_http_requests_total Total HTTP requests completed.")
            .AppendLine("# TYPE eduplatform_http_requests_total counter")
            .AppendLine($"eduplatform_http_requests_total {requests}")
            .AppendLine("# HELP eduplatform_http_errors_total Total HTTP requests returning a 5xx status.")
            .AppendLine("# TYPE eduplatform_http_errors_total counter")
            .AppendLine($"eduplatform_http_errors_total {errors}")
            .AppendLine("# HELP eduplatform_http_active_requests Current HTTP requests in progress.")
            .AppendLine("# TYPE eduplatform_http_active_requests gauge")
            .AppendLine($"eduplatform_http_active_requests {activeRequests}")
            .AppendLine("# HELP eduplatform_http_request_duration_milliseconds_sum Total HTTP request duration in milliseconds.")
            .AppendLine("# TYPE eduplatform_http_request_duration_milliseconds_sum counter")
            .AppendLine($"eduplatform_http_request_duration_milliseconds_sum {durationMilliseconds}")
            .AppendLine("# HELP eduplatform_http_request_duration_milliseconds_average Average HTTP request duration in milliseconds.")
            .AppendLine("# TYPE eduplatform_http_request_duration_milliseconds_average gauge")
            .AppendLine($"eduplatform_http_request_duration_milliseconds_average {averageDuration:0.###}");

        return output.ToString();
    }
}