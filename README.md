# ChartQuery .NET SDK

The official .NET SDK for the [ChartQuery](https://www.chartquery.com) chart and QR code API.

Generate chart images, diagrams, barcodes, QR codes and thermal labels on demand. PNG, SVG or PDF output.

The SDK exposes **20 typed async methods**, one per documented endpoint (with `required` properties checked at compile time), plus a generic `RequestAsync`.

## Requirements

- .NET 8 or newer

## Installation

```sh
dotnet add package ChartQuery
```

## Get your API key

[Sign up for ChartQuery](https://app.chartquery.com/register) or sign in, then copy your API key from the [dashboard](https://app.chartquery.com). `new ChartQueryClient()` reads the `CHARTQUERY_API_KEY` environment variable.

## Quick start

```csharp
using ChartQuery;

using var client = new ChartQueryClient();
var response = await client.ChartAiAsync(new ChartAiParams { Prompt = "Monthly revenue 2026 as a bar chart: Jan 10, Feb 14, Mar 18" });
Console.WriteLine(response.Json);
```

Parameter classes mirror the [API reference](https://app.chartquery.com/openapi). Use `response.Deserialize<MyType>()` to map the JSON onto your own types.

## Binary responses

Endpoints that return a file (image, PDF…) give you the bytes; `IsJson` tells both cases apart:

```csharp
var response = await client.QrcodeAsync(new QrcodeParams { Data = "https://chartquery.com", Format = "png" });
if (!response.IsJson)
{
    await File.WriteAllBytesAsync("qrcode.png", response.Bytes);
}
```

## Async jobs

Any call accepts `is_async: true` (useful past the 60-second server limit): the response is then a `job_id`. `WaitForJobAsync` polls `GET /v1/jobs/{id}` until a terminal status (`CompletedJob`, `Error`, `NotFound`, `BadRequest`):

```csharp
var submitted = await client.ChartAiAsync(new ChartAiParams { Prompt = "Monthly revenue 2026 as a bar chart: Jan 10, Feb 14, Mar 18", IsAsync = true });
var job = await client.WaitForJobAsync(
    submitted.Json.GetProperty("job_id").GetString()!,
    onProgress: j => Console.WriteLine($"{j.GetProperty("status")} {j.GetProperty("progress")}%"));
```

## GET or POST

Methods use GET when the endpoint supports it (query string). POST sends the same parameters as JSON:

```csharp
await client.BarcodeAsync(new BarcodeParams { Data = "test" }, new RequestOptions { Method = "POST" });
```

## Any endpoint

```csharp
var response = await client.RequestAsync("GET", "/v1/usage");
```

## Errors

```csharp
try
{
    await client.ChartAiAsync(new ChartAiParams { Prompt = "Monthly revenue 2026 as a bar chart: Jan 10, Feb 14, Mar 18" });
}
catch (ChartQueryApiException exception)
{
    Console.Error.WriteLine($"{exception.StatusCode}: {exception.Message}");
}
```

| Exception | When |
| --- | --- |
| `ChartQueryConfigurationException` | Missing API key, invalid options or unsupported method. |
| `ChartQuerySerializationException` | A required parameter is missing or a value can't be sent. |
| `ChartQueryTransportException` | Network failure or timeout. |
| `ChartQueryApiException` | Non-2xx status (`StatusCode`, `ErrorMessage`, `ResponseBody`, `RawResponse`). |
| `ChartQueryResponseDecodingException` | A success response could not be decoded. |

All of them derive from `ChartQueryException`. Caller cancellation surfaces as `OperationCanceledException`.

## Retries

Requests are retried up to 3 times after a network error, a `408`, `500`, `502`, `503` or `504`, or a rate limit — which the API returns as `401` (or `429`) with a `Rate limit exceeded` message. Per-key quota errors (`…: quota daily`) are not retried, nor are `400`, `402`, `404` or client-side timeouts. The delay starts at 1 second and grows 1.5× per retry (capped at 30 seconds); a `Retry-After` header wins.

```csharp
using var client = new ChartQueryClient(new ChartQueryClientOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromMilliseconds(500) });
await client.ChartAiAsync(new ChartAiParams { Prompt = "Monthly revenue 2026 as a bar chart: Jan 10, Feb 14, Mar 18" }, new RequestOptions { MaxRetries = 0 }); // one request without retries
```

## Batch requests

`BatchAsync` runs a function per item with at most 10 in flight (`concurrency`) and returns one `BatchResult` per item, in input order. A failing item never stops the others.

```csharp
string[] inputs = { "Monthly revenue 2026 as a bar chart: Jan 10, Feb 14, Mar 18", "Market share as a pie chart: A 40, B 35, C 25" };
var results = await client.BatchAsync(inputs, (value, ct) =>
    client.ChartAiAsync(new ChartAiParams { Prompt = value }, cancellationToken: ct), concurrency: 5);
foreach (var result in results)
{
    if (result.Ok)
    {
        Console.WriteLine(result.Value!.Json);
    }
    else
    {
        Console.Error.WriteLine($"{inputs[result.Index]}: {result.Error!.Message}");
    }
}
```

| Constant | Default | Description |
| --- | --- | --- |
| `ChartQueryClient.DefaultConcurrency` | `10` | Tasks run at the same time by batch |
| `ChartQueryClient.DefaultTimeout` | `TimeSpan.FromSeconds(120)` | Request timeout |
| `ChartQueryClient.MaxRetries` | `3` | Retry attempts after a network error, a 408/5xx or a rate limit |
| `ChartQueryClient.RetryBackoffFactor` | `1.5` | Exponential backoff multiplier |
| `ChartQueryClient.DefaultRetryDelay` | `TimeSpan.FromSeconds(1)` | Delay before the first retry |

## Configuration

```csharp
using var client = new ChartQueryClient(new ChartQueryClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("CHARTQUERY_API_KEY"),
    Timeout = TimeSpan.FromSeconds(60), // default 120 s
    HttpClient = myHttpClient,           // optional, you keep ownership
});
```

## Endpoints

20 methods, generated from the [OpenAPI document](https://app.chartquery.com/openapi).

| Method | HTTP | Description |
| --- | --- | --- |
| `BarcodeAsync` | GET / POST `/v1/barcode` | Generate a barcode image via GET. Output: SVG (default) or PNG. Supports 25+ formats: EAN-13, Code 128, ITF-14, GS1-128, postal codes and m… |
| `ChartAsync` | GET / POST `/v1/chart` | Render a Chart.js image via GET. Pass the full chart object as URL-encoded JSON in query. Prefer format png for ChatGPT. POST sends the sam… |
| `ChartAiAsync` | GET / POST `/v1/chart/ai` | Describe a chart in plain English via GET and get a rendered image back. Powered by AI. |
| `ChartRenderAsync` | GET `/v1/chart/render/{id}` | Render a previously saved chart by ID. Public, no API key required. Supports URL overrides for templates (?data1=, ?labels=, ?title=…). |
| `ChartViewAsync` | GET `/v1/chart/view/{id}` | View a previously saved chart as an interactive HTML page. Public, no API key required. Iframe-embeddable. |
| `DiagramAsync` | GET / POST `/v1/diagram` | Render a diagram via GET. Use type (mermaid, d2, …), query (source code), and format=png for ChatGPT. POST uses diagram_type and diagram_so… |
| `DiagramAiAsync` | GET / POST `/v1/diagram/ai` | Describe a diagram in plain English via GET and get a rendered image back. Powered by AI. |
| `DiagramRenderAsync` | GET `/v1/diagram/render/{id}` | Render a previously saved diagram by ID. Public, no API key required. Supports ?format= and ?query= overrides. |
| `DiagramViewAsync` | GET `/v1/diagram/view/{id}` | View a previously saved diagram as an HTML page. Public, no API key required. Iframe-embeddable with inline SVG rendering. |
| `JobsAsync` | GET `/v1/jobs/{id}` | Poll the status, progress (0–100) and result of a job started with is_async=true. Polling is free. Poll until status is terminal: Completed… |
| `LabelAsync` | GET / POST `/v1/label` | Preview a thermal label via GET. Pass the same label object as a URL-encoded JSON string in query. Inside that JSON, text items use content… |
| `LabelCompileAsync` | POST `/v1/label/compile` | Compile a label to printer commands (tsc, zpl, epl, …). POST width, language, and elements. Text uses content and size (1-10), not text or… |
| `LabelConvertAsync` | POST `/v1/label/convert` | Convert printer commands from one language to another. POST source, from, and to. from is the source language (tsc, zpl, epl, …). to is the… |
| `LabelParseAsync` | POST `/v1/label/parse` | Parse printer commands into structured elements. POST source and language (tsc, zpl, epl, …). |
| `LabelRenderAsync` | GET `/v1/label/render/{id}` | Render a previously saved label by ID. Public, no API key required. Supports ?format= override. |
| `LabelValidateAsync` | POST `/v1/label/validate` | Validate printer commands. POST source (the raw commands) and language (tsc, zpl, epl, …). |
| `LabelViewAsync` | GET `/v1/label/view/{id}` | View a previously saved label as an HTML page. Public, no API key required. Iframe-embeddable with inline SVG rendering. |
| `QrcodeAsync` | GET / POST `/v1/qrcode` | Generate a styled QR code via GET. Output: SVG (default) or PNG. Supports dot styles, gradients, custom corners and all QR versions. |
| `StatsAsync` | GET `/v1/stats` | Infrastructure health for an engine or endpoint over the last hour. Not scoped to your account or tenant. Enabled per account: HTTP 403 mea… |
| `UsageAsync` | GET `/v1/usage` | Return this account's Chartquery usage and credit balance. No body. Requires an API key. |

## All ChartQuery SDKs

| SDK | Package | Install |
| --- | --- | --- |
| Node.js | [`@chartquery/sdk`](https://www.npmjs.com/package/@chartquery/sdk) | `npm install @chartquery/sdk` |
| Python | [`chartquery`](https://pypi.org/project/chartquery/) | `pip install chartquery` |
| Go | [`chartquery-go`](https://pkg.go.dev/github.com/chartquery/chartquery-go) | `go get github.com/chartquery/chartquery-go` |
| .NET | [`ChartQuery`](https://www.nuget.org/packages/ChartQuery) | `dotnet add package ChartQuery` |
| Ruby | [`chartquery`](https://rubygems.org/gems/chartquery) | `gem install chartquery` |
| PHP | [`chartquery/sdk`](https://packagist.org/packages/chartquery/sdk) | `composer require chartquery/sdk` |
| Java | [`chartquery-java`](https://github.com/chartquery/chartquery-java) | build from source, see its README |
| CLI | [`@chartquery/cli`](https://www.npmjs.com/package/@chartquery/cli) | `npm install -g @chartquery/cli` |
| MCP server | [`@chartquery/mcp`](https://www.npmjs.com/package/@chartquery/mcp) | `npx -y @chartquery/mcp` |

## License

MIT — see [LICENSE](LICENSE).
