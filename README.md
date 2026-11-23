# Llama on Windows with .NET

A .NET 11 file-based chat app for models running locally in [Llama](https://llama.app/).
It streams responses and gives the model two tools: `GetMachineInfo` and `ListModels`.

With the .NET 11 SDK installed and Llama running with a downloaded model:

```powershell
dotnet run llama-chat.cs
```

See the [blog post](https://devblogs.microsoft.com/dotnet/local-models-dotnet-llama-windows/) for installation,
model selection, and an explanation of the code.
