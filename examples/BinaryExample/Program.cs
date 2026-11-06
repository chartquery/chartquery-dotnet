using ChartQuery;

using var client = new ChartQueryClient(); // reads CHARTQUERY_API_KEY
var response = await client.QrcodeAsync(new QrcodeParams { Data = "https://chartquery.com", Format = "png" });
if (response.IsJson)
{
    Console.WriteLine(response.Json);
}
else
{
    await File.WriteAllBytesAsync("qrcode.png", response.Bytes);
    Console.WriteLine($"Saved qrcode.png ({response.ContentType}, {response.Bytes.Length} bytes)");
}
