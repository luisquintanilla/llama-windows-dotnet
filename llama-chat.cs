#!/usr/bin/env dotnet
#:package Microsoft.Extensions.AI@11.0.0
#:package Microsoft.Extensions.AI.OpenAI@11.0.0

using System.ClientModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;

// Llama serves an OpenAI-compatible API on localhost. Set LLAMA_ENDPOINT if you changed the port.
var endpoint = Environment.GetEnvironmentVariable("LLAMA_ENDPOINT") ?? "http://localhost:9931/v1";
using var http = new HttpClient();

// Ask Llama which models it can serve. This also tells us whether it's running.
List<string> models;
try
{
    models = [.. (await GetLlamaModelsAsync()).Select(m => m.GetProperty("id").GetString()!)];
}
catch (HttpRequestException)
{
    Console.WriteLine($"Couldn't reach Llama at {endpoint}. Open Llama from the Start menu and try again.");
    return 1;
}

if (models.Count == 0)
{
    Console.WriteLine("Llama doesn't have any models yet. Open Llama from the system tray and download a model.");
    return 1;
}

Console.WriteLine("Models available in Llama:");
models.ForEach(m => Console.WriteLine($"  {m}"));

// Use the model passed as an argument, or the first one Llama reports.
var model = args.Length > 0 ? args[0] : models[0];
Console.WriteLine($"\nChatting with {model}. Press Enter on an empty line to exit.");

// No API key is required by default, but the OpenAI client expects a value.
IChatClient client = new OpenAIClient(new ApiKeyCredential("no-key-required"), new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
    .GetChatClient(model)
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var options = new ChatOptions
{
    Tools = [AIFunctionFactory.Create(GetMachineInfo), AIFunctionFactory.Create(ListModels)]
};

List<ChatMessage> history = [new(ChatRole.System, "You are a helpful assistant running locally on the user's PC. Keep answers short.")];

while (true)
{
    Console.Write("\nYou: ");
    var prompt = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(prompt))
    {
        break;
    }

    history.Add(new(ChatRole.User, prompt));

    Console.Write("Llama: ");
    List<ChatResponseUpdate> updates = [];
    await foreach (var update in client.GetStreamingResponseAsync(history, options))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }
    Console.WriteLine();

    history.AddMessages(updates);
}

return 0;

// Calls GET /v1/models. Llama adds details such as load status to the standard OpenAI response.
async Task<List<JsonElement>> GetLlamaModelsAsync()
{
    using var json = JsonDocument.Parse(await http.GetStringAsync($"{endpoint}/models"));
    return [.. json.RootElement.GetProperty("data").EnumerateArray().Select(m => m.Clone())];
}

[Description("Lists the models downloaded in Llama on this PC, whether each one is loaded in memory, and which one the user is chatting with.")]
async Task<string> ListModels()
{
    var lines = (await GetLlamaModelsAsync()).Select(m =>
    {
        var id = m.GetProperty("id").GetString();
        var status = m.TryGetProperty("status", out var s) ? s.GetProperty("value").GetString() : "unknown";
        return $"{id} ({status}){(id == model ? ", current chat" : "")}";
    });
    return string.Join(Environment.NewLine, lines);
}

[Description("Gets the name, operating system, processor count, and .NET version of the computer the user is chatting from.")]
static string GetMachineInfo() =>
    $"{Environment.MachineName}, {RuntimeInformation.OSDescription}, {Environment.ProcessorCount} logical processors, .NET {Environment.Version}";
