namespace APIBenchmark;

public class Options
{
    public int WarmupIterations { get; set; }
    public int NumberOfRequests { get; set; }
    public bool HasWarmup { get; set; }
    public int LeadingDelayInMilliseconds { get; set; }
    public bool ExportResults { get; set; }
    public int? InitialBucket { get; set; }
    public bool IsLoadTest { get; set; }
    public LatencyOptions LatencyOptions { get; set; }
    public LoadOptions LoadOptions { get; set; }
    public DisplayOptions DisplayOptions { get; set; }
}

public class LoadOptions
{
    public int Increment { get; set; }
    public bool HasRampUp { get; set; }
    
    public RequestParams DefaultRequest { get; set; } 
    public List<string> Variables { get; set; }
}

public class LatencyOptions
{
    public List<RequestParams> Requests { get; set; } = null!;
}

public class DisplayOptions
{
    public int BucketSizeInMilliseconds { get; set; }
}