namespace APIBenchmark;

public class LatencyOptions
{
    public int WarmupIterations { get; set; }
    public int NumberOfRequests { get; set; }
    public bool HasWarmup { get; set; }
    public int LeadingDelayInMilliseconds { get; set; }
    public List<RequestParams> Requests { get; set; }
    public bool ExportResults { get; set; }
}