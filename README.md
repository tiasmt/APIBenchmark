# APIBenchmark

*A C# API Performance Benchmarking Tool*

Tool designed to measure and analyze the performance of APIs. It allows you to benchmark your API's response times and generate statistics, providing insights into its characteristics.

## Features:

Benchmark multiple APIs: Compare the performance of different APIs, allowing for side-by-side analysis.
- Flexible configuration: Configure the benchmark parameters easily through an appsettings.json file.
  - Configurable number of requests per endpoint
  - Perform warm-up iterations before starting the benchmark (to eliminate potential cold-start effects)
  - Add a leading delay between requests
  - Export results to a file
- Statistical analysis: Generate comprehensive reports with various statistical measures, including:
  - Average: The mean response time across all requests
  - Median: The middle value in the dataset, representing the 50th percentile
  - Minimum: The fastest response time recorded
  - Maximum: The slowest response time recorded
  - 90th percentile: The response time at which 90% of requests are faster
  - 95th percentile: The response time at which 95% of requests are faster
  - 99th percentile: The response time at which 99% of requests are faster
 

## Statistics / Results:
 
  <img width="1780" alt="image" src="https://github.com/tiasmt/APIBenchmark/assets/20759400/d149ec0d-b1b8-47b2-9c3f-052db7ed6e7d">

