namespace APIBenchmark;

public class Options
{
    public int WarmupIterations { get; set; }
    public int NumberOfRequests { get; set; }
    public bool HasWarmup { get; set; }
    public int LeadingDelayInMilliseconds { get; set; }
    public List<RequestParams> Requests { get; set; }
    public bool ExportResults { get; set; }
    public int? InitialBucket { get; set; }
    public bool IsLoadTest { get; set; }
    public LoadOptions LoadOptions { get; set; }
    public DisplayOptions DisplayOptions { get; set; }
}

public class LoadOptions
{
    public int IncrementalBuckets { get; set; }
}

public class DisplayOptions
{
    public int BucketSizeInMilliseconds { get; set; }
}