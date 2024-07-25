using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using HdrHistogram;
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
    private readonly Options _options;

    public ApiLatency(IOptions<Options> options)
    {
        _options = options.Value;
    }

    public async Task Process()
    {
        PrintTitle();

        if (!_options.IsLoadTest)
            await LatencyProcess();
        else
            await LoadProcess();
    }

    private async Task LatencyProcess()
    {
        foreach (var requestParam in _options.LatencyOptions.Requests)
        {
            var latencies = new List<TimeSpan>(_options.NumberOfRequests);

            if (_options.HasWarmup)
                await Warmup(requestParam);

            AnsiConsole.Foreground = Color.Grey58;
            AnsiConsole.WriteLine($"Testing latency for: {requestParam.Endpoint}");

            var histogram = new LongHistogram(TimeStamp.Hours(1), 3);

            for (var i = 0; i < _options.NumberOfRequests; i++)
            {

                var request = CreateRequest(requestParam);
                await Task.Delay(_options.LeadingDelayInMilliseconds);

                var startTimestamp = Stopwatch.GetTimestamp();
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");

                stopwatch.Stop();
                var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
                histogram.RecordValue(elapsed);
                var elapsedTime = stopwatch.Elapsed;
                AnsiConsole.MarkupLine($"[{DisplayConstants.Secondary}]Request[/] {i + 1} took {elapsedTime.TotalMilliseconds} ms");
                latencies.Add(elapsedTime);
            }

            AnsiConsole.Clear();
            var orderedLatencies = latencies.OrderBy(x => x).ToList();
            DisplayChart(orderedLatencies, requestParam.Name);
            var statistics = CalculateStatistics(orderedLatencies);
            DisplayStatistics(statistics);
            DisplaySummary(requestParam.Name, statistics.Total.ToString(CultureInfo.InvariantCulture));

            if (_options.ExportResults)
                ExportToCsv(requestParam.Name, statistics);

            //var scalingRatio = OutputScalingFactor.TimeStampToMilliseconds;
            //histogram.OutputPercentileDistribution(
            //  Console.Out,
            //  outputValueUnitScalingRatio: scalingRatio);
        }
    }

    private async Task LoadProcess()
    {
        AnsiConsole.Foreground = Color.Grey58;

        var latencies = new List<TimeSpan>(_options.NumberOfRequests);
        var requestTasks = new List<Task<HttpResponseMessage>>();

        var defaultRequest = _options.LoadOptions.DefaultRequest;
        
        AnsiConsole.WriteLine($"Testing load for: {defaultRequest.Type} | {defaultRequest.Name} | {defaultRequest.Endpoint}");
        int incrementCount = 0;
        var iteration = 1;
        while (incrementCount != _options.LoadOptions.Variables.Count)
        {
            var stopwatch = Stopwatch.StartNew();
            foreach (var requestParam in _options.LoadOptions.Variables)
            {
                defaultRequest.Body += requestParam;
                var request = CreateRequest(defaultRequest);
                var requestTask = _httpClient.SendAsync(request);
                requestTasks.Add(requestTask);
                incrementCount++;
                if (incrementCount >= _options.LoadOptions.Increment * iteration)
                    break;
            }

            var responses = await Task.WhenAll(requestTasks);
            var failedResponseMessages = responses.Where(x => !x.IsSuccessStatusCode);

            foreach (var failedResponseMessage in failedResponseMessages)
                AnsiConsole.MarkupLine($"[red]Error: {failedResponseMessage.RequestMessage}[/]");

            stopwatch.Stop();
            var elapsedTime = stopwatch.Elapsed;
            AnsiConsole.MarkupLine(
                $"[{DisplayConstants.Secondary}]Batch of [/] {incrementCount} requests took {elapsedTime.TotalMilliseconds} ms");
            incrementCount = 0;     
            iteration++;
        }

        Console.ReadLine();
        AnsiConsole.Clear();
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
                    for (var i = 0; i < _options.WarmupIterations; i++)
                    {
                        var request = CreateRequest(requestParam);
                        await _httpClient.SendAsync(request);
                        // Increment
                        warmupTask.Increment((double)100 / _options.WarmupIterations);
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
        var percentile999 = CalculatePercentile(orderedLatencies, 0.999);
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
            Percentile999 = percentile999,
            Percentile99 = percentile99,
            Percentile95 = percentile95,
            Percentile90 = percentile90
        };
    }

    private void DisplayChart(IEnumerable<TimeSpan> orderedLatencies, string requestName)
    {
        PrintTitle();
        Padding();

        var bucketSize = _options.DisplayOptions.BucketSizeInMilliseconds;
        var lookup = orderedLatencies.ToLookup(x => (int)x.TotalMilliseconds / bucketSize);
        var minimum = _options.InitialBucket ?? lookup.Min(x => x.Key);
        var maximum = lookup.Max(x => x.Key);
        var histogram = Enumerable.Range(minimum, maximum - minimum + 1)
            .Select(x => new { Range = $"{x * bucketSize}-{(x + 1) * bucketSize}", Count = lookup[x].Count(), });

        // Render bar chart
        AnsiConsole.Write(new BarChart()
            .Width(1000)
            .Label($"[bold underline]Histogram: [{DisplayConstants.Secondary}]{requestName}[/][/]")
            .CenterLabel()
            .AddItems(histogram, (item) => new BarChartItem(
                item.Range, item.Count, Color.DarkKhaki)));

        Padding();
    }

    private static void DisplayStatistics(Statistics statistics)
    {
        SubTitle("Statistics");

        // Create a table
        var table = new Table();
        table.Expand();

        table.AddColumn("Stats").Centered();
        table.AddColumn("Percentiles").Centered();

        // Add some rows
        table.AddRow(
            DisplayStat(statistics.Average, "average"),
            DisplayPercentile(statistics.Percentile90, "90"));

        table.AddRow(
            DisplayStat(statistics.Median, "median"),
            DisplayPercentile(statistics.Percentile95, "95"));

        table.AddRow(
            DisplayStat(statistics.Minimum, "minimum"),
            DisplayPercentile(statistics.Percentile99, "99"));

        table.AddRow(
            DisplayStat(statistics.Maximum, "maximum"),
            DisplayPercentile(statistics.Percentile999, "99.9"));

        table.AddRow(
            DisplayStat(statistics.Total, "total"),
            string.Empty);

        AnsiConsole.Write(table);
    }

    private void DisplaySummary(string requestName, string totalTime)
    {
        SubTitle("Summary");

        // Create a table
        var table = new Table();
        table.Expand();

        table.AddColumn("").Centered();
        table.AddColumn("").Centered();
        table.HideHeaders();

        // Add some rows
        table.AddRow(
            "Exported File",
            $"{Prefix()} {requestName}_latency.csv");

        table.AddRow(
            "Total Requests",
            $"{Prefix()} {_options.NumberOfRequests.ToString()}");

        table.AddRow(
            "Test Run Time",
            $"{Prefix()} {totalTime} [{DisplayConstants.Secondary}]seconds[/]");

        AnsiConsole.Write(table);
    }

    private static string Prefix(char prefix = '|')
        => $"[{DisplayConstants.Primary}]{prefix}[/]";

    private static string DisplayStat(double value, string label)
        => $"{Prefix()} {value:F} [{DisplayConstants.Secondary}]{label}[/]";
    private static string DisplayPercentile(double percentile, string label)
        => $"{Prefix()} {percentile:F} [{DisplayConstants.Secondary}]{label}th[/]";

    private static double CalculateMedian(IReadOnlyList<TimeSpan> orderedLatencies) =>
        (orderedLatencies[orderedLatencies.Count / 2].TotalMilliseconds +
         orderedLatencies[(orderedLatencies.Count + 1) / 2].TotalMilliseconds) / 2;

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
            request.Content = new StringContent(requestParams.Body, null, "application/json");
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
        csv.AppendLine($"99.9th, {statistics.Percentile999}");
        var filePath = $"{requestName}_latency.csv";
        File.WriteAllText(filePath, csv.ToString());
        AnsiConsole.MarkupLine($"[{DisplayConstants.Secondary}]Exported to {filePath}[/]");
    }

    private readonly struct Statistics
    {
        internal double Minimum { get; init; }
        internal double Maximum { get; init; }
        internal double Average { get; init; }
        internal double Total { get; init; }
        internal double Median { get; init; }
        internal double Percentile999 { get; init; }
        internal double Percentile99 { get; init; }
        internal double Percentile95 { get; init; }
        internal double Percentile90 { get; init; }
    }

    private static void Padding()
    {
        Console.WriteLine();
        Console.WriteLine();
    }

    private static void SubTitle(string title)
    {
        var panel = new Panel($"[{DisplayConstants.Secondary}]{title}[/]")
        {
            Border = BoxBorder.Ascii,
        };

        AnsiConsole.Write(panel);
    }
}

internal static class DisplayConstants
{
    internal const string Primary = "yellow";
    internal const string Secondary = "grey";
}
