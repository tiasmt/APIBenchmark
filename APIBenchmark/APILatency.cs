using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace APIBenchmark;

public interface IApiLatency
{
    Task Process();
}

public class ApiLatency : IApiLatency
{
    private readonly HttpClient _httpClient = new();
    private readonly LatencyOptions _latencyOptions;

    public ApiLatency(IOptions<LatencyOptions> options)
    {
        _latencyOptions = options.Value;
    }

    public async Task Process()
    {
        PrintTitle();

        foreach (var requestParam in _latencyOptions.Requests)
        {
            var latencies = new List<TimeSpan>(_latencyOptions.NumberOfRequests);

            if (_latencyOptions.HasWarmup)
                await Warmup(requestParam);

            AnsiConsole.Foreground = Color.Grey58;
            AnsiConsole.WriteLine($"Testing latency for: {requestParam.Endpoint}");

            for (var i = 0; i < _latencyOptions.NumberOfRequests; i++)
            {
                var request = CreateRequest(requestParam);
                await Task.Delay(_latencyOptions.LeadingDelayInMilliseconds);

                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");

                stopwatch.Stop();
                var elapsedTime = stopwatch.Elapsed;
                AnsiConsole.MarkupLine($"[grey58]Request {i + 1} took {elapsedTime.TotalMilliseconds} ms[/]");
                latencies.Add(elapsedTime);
            }

            AnsiConsole.Clear();
            var orderedLatencies = latencies.OrderBy(x => x).ToList();
            DisplayChart(orderedLatencies, requestParam.Name);
            var statistics = CalculateStatistics(orderedLatencies);
            DisplaySummary(statistics);

            if (_latencyOptions.ExportResults)
                ExportToCsv(requestParam.Name, statistics);
        }
    }

    private static void PrintTitle()
    {
        AnsiConsole.Write(
            new FigletText("Latency Benchmark")
                .Centered()
                .Color(Color.Grey82));
    }

    private async Task Warmup(RequestParams requestParam)
    {
        // Asynchronous
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                // Define tasks
                var warmupTask = ctx.AddTask("Warming up ...");

                while (!ctx.IsFinished)
                {
                    for (var i = 0; i < _latencyOptions.WarmupIterations; i++)
                    {
                        var request = CreateRequest(requestParam);
                        await _httpClient.SendAsync(request);
                        // Increment
                        warmupTask.Increment((double)100 / _latencyOptions.WarmupIterations);
                    }
                }
            });
    }

    private static Statistics CalculateStatistics(List<TimeSpan> orderedLatencies)
    {
        var minimum = orderedLatencies[0].TotalMilliseconds;
        var maximum = orderedLatencies[^1].TotalMilliseconds;
        var average = orderedLatencies.Average(x => x.TotalMilliseconds);
        var total = orderedLatencies.Sum(x => x.TotalMilliseconds);
        var median = CalculateMedian(orderedLatencies);
        var percentile99 = CalculatePercentile(orderedLatencies, 0.99);
        var percentile95 = CalculatePercentile(orderedLatencies, 0.95);
        var percentile90 = CalculatePercentile(orderedLatencies, 0.90);

        return new Statistics
        {
            Minimum = minimum,
            Maximum = maximum,
            Average = average,
            Total = total,
            Median = median,
            Percentile99 = percentile99,
            Percentile95 = percentile95,
            Percentile90 = percentile90
        };
    }

    private void DisplayChart(IEnumerable<TimeSpan> orderedLatencies, string requestName)
    {
        PrintTitle();
        Padding();

        var bucketSize = _latencyOptions.BucketSizeInMilliseconds;
        var lookup = orderedLatencies.ToLookup(x => (int) x.TotalMilliseconds / bucketSize);
        var minimum = _latencyOptions.InitialBucket ?? lookup.Min(x => x.Key);
        var maximum = lookup.Max(x => x.Key);
        var histogram = Enumerable.Range(minimum, maximum - minimum + 1)
            .Select(x => new {Range = $"{x * bucketSize}-{(x + 1) * bucketSize}", Count = lookup[x].Count(),});

        // Render bar chart
        AnsiConsole.Write(new BarChart()
            .Width(1000)
            .Label($"[bold underline]Latencies {requestName}[/]")
            .CenterLabel()
            .AddItems(histogram, (item) => new BarChartItem(
                item.Range, item.Count, Color.DarkKhaki)));

        Padding();
    }


    private static void DisplaySummary(Statistics statistics)
    {
        // Create a table
        var table = new Table();
        table.Expand();

        // Add some columns
        table.AddColumn("Minimum").Centered();
        table.AddColumn("Maximum").Centered();
        table.AddColumn("Average").Centered();
        table.AddColumn("Total").Centered();
        table.AddColumn("Median").Centered();
        table.AddColumn("90th").Centered();
        table.AddColumn("95th").Centered();
        table.AddColumn("99th").Centered();
        
        // Add some rows
        table.AddRow(
            statistics.Minimum.ToString("F"),
            statistics.Maximum.ToString("F"),
            statistics.Average.ToString("F"),
            statistics.Total.ToString("F"),
            statistics.Median.ToString("F"),
            statistics.Percentile90.ToString("F"),
            statistics.Percentile95.ToString("F"),
            statistics.Percentile99.ToString("F")
        );

        // Render the table to the console
        AnsiConsole.Write(table);
    }

    private static double CalculateMedian(IReadOnlyList<TimeSpan> orderedLatencies) =>
        (orderedLatencies[orderedLatencies.Count / 2].TotalMilliseconds + orderedLatencies[(orderedLatencies.Count + 1) / 2].TotalMilliseconds) / 2;

    private static double CalculatePercentile(IReadOnlyList<TimeSpan> orderedLatencies, double percentile)
    {
        var index = (int)Math.Floor(orderedLatencies.Count * percentile);
        return orderedLatencies[index - 1].TotalMilliseconds;
    }

    private static HttpRequestMessage CreateRequest(RequestParams requestParams)
    {
        var request = new HttpRequestMessage(requestParams.Type.ToHttpMethod(), requestParams.Endpoint);

        if (!string.IsNullOrEmpty(requestParams.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestParams.BearerToken);
        }

        if (!string.IsNullOrEmpty(requestParams.Body))
        {
            request.Content = new StringContent(requestParams.Body, Encoding.UTF8, "application/json");
        }

        if (requestParams.Headers is null || requestParams.Headers?.Count <= 0) return request;

        foreach (var (key, value) in requestParams.Headers!)
        {
            request.Headers.Add(key, value);
        }

        return request;
    }

    private static void ExportToCsv(string requestName, Statistics statistics)
    {
        var csv = new StringBuilder();
        csv.AppendLine($"Minimum, {statistics.Minimum}");
        csv.AppendLine($"Maximum, {statistics.Maximum}");
        csv.AppendLine($"Average, {statistics.Average}");
        csv.AppendLine($"Total, {statistics.Total}");
        csv.AppendLine($"Median, {statistics.Median}");
        csv.AppendLine($"90th, {statistics.Percentile90}");
        csv.AppendLine($"95th, {statistics.Percentile95}");
        csv.AppendLine($"99th, {statistics.Percentile99}");
        var filePath = $"{requestName}_latency.csv";
        File.WriteAllText(filePath, csv.ToString());
        AnsiConsole.MarkupLine($"[grey58]Exported to {filePath}[/]");
    }

    private readonly struct Statistics
    {
        internal double Minimum { get; init; }
        internal double Maximum { get; init; }
        internal double Average { get; init; }
        internal double Total { get; init; }
        internal double Median { get; init; }
        internal double Percentile99 { get; init; }
        internal double Percentile95 { get; init; }
        internal double Percentile90 { get; init; }
    }

    private static void Padding()
    {
        Console.WriteLine();
        Console.WriteLine();
    }
}
