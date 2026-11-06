using ChartQuery;

using var client = new ChartQueryClient(); // reads CHARTQUERY_API_KEY
var submitted = await client.ChartAiAsync(new ChartAiParams { Prompt = "Monthly revenue 2026 as a bar chart: Jan 10, Feb 14, Mar 18", IsAsync = true });
var job = await client.WaitForJobAsync(
    submitted.Json.GetProperty("job_id").GetString()!,
    onProgress: j => Console.WriteLine($"{j.GetProperty("status")} {j.GetProperty("progress")}%"));
Console.WriteLine(job.Json);
