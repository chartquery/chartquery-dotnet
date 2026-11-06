using ChartQuery;

using var client = new ChartQueryClient(); // reads CHARTQUERY_API_KEY
var response = await client.ChartAiAsync(new ChartAiParams { Prompt = "Monthly revenue 2026 as a bar chart: Jan 10, Feb 14, Mar 18" });
Console.WriteLine(response.Json);
