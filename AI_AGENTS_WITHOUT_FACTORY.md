# Demo 1 - Single Agent Resume Ingestion (Without AgentFactory)

A complete from-scratch guide for building the **Demo 1** pipeline using
[Microsoft Agents Framework](https://learn.microsoft.com/en-us/azure/ai-services/agents/overview)
and Azure OpenAI **without** any factory helper class.

---

## What We Are Building

A .NET 8 console application that:

1. Reads a plain-text resume from disk.
2. Sends it to an **Azure OpenAI** model through the **Microsoft Agents Framework**.
3. Parses the structured JSON response into a typed `ResumeProfile` object.
4. Prints the candidate summary to the console.

The agent is created **inline** - no `AgentFactory`, no abstraction layer.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | `dotnet --version` must return `8.x` |
| Azure OpenAI resource | A deployed chat model (e.g. `gpt-4o`) |
| Azure CLI (`az`) | Only needed for keyless auth - run `az login` once |

---

## Project Layout (end state)

```
InterviewAssistant/
├── assets/
│   └── resumes/
│       └── jane_doe.txt
├── Agents/
│   ├── AgentPrompts.cs
│   └── JsonAgentRunner.cs
├── Models/
│   └── ResumeProfile.cs
├── InterviewAssistant.csproj
└── Program.cs
```

---

## Step 1 - Create the Console Project

```pwsh
dotnet new console -n InterviewAssistant -f net8.0
cd InterviewAssistant
```

---

## Step 2 - Add NuGet Packages

```pwsh
dotnet add package Azure.AI.OpenAI               --version 2.8.0-beta.1
dotnet add package Azure.Identity                --version 1.17.1
dotnet add package Microsoft.Agents.AI.OpenAI    --version 1.0.0-preview.260212.1
dotnet add package Microsoft.Agents.AI.Workflows --version 1.0.0-preview.260212.1
```

| Package | Purpose |
|---|---|
| `Azure.AI.OpenAI` | `AzureOpenAIClient` and `GetChatClient()` |
| `Azure.Identity` | `AzureKeyCredential` / `AzureCliCredential` |
| `Microsoft.Agents.AI.OpenAI` | `AsAIAgent()` extension method on `ChatClient` |
| `Microsoft.Agents.AI.Workflows` | `AIAgent` type and `RunAsync()` |

---

## Step 3 - Set Environment Variables

The application reads three variables at runtime. Set them in your shell before running:

```pwsh
$env:AZURE_OPENAI_ENDPOINT   = "https://<your-resource>.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT = "<your-deployment-name>"   # e.g. gpt-4o
$env:AZURE_OPENAI_API_KEY    = "<your-api-key>"           # omit to use az login
```

> If `AZURE_OPENAI_API_KEY` is **not** set the app falls back to `AzureCliCredential`
> (requires an active `az login` session).

---

## Step 4 - Add a Sample Resume

Create `assets/resumes/jane_doe.txt`:

```
Jane Doe
Seattle, WA | jane.doe@email.com | github.com/janedoe

SUMMARY
Backend-focused software engineer with 6+ years building cloud services in C#/.NET and Azure.
Strong in APIs, distributed systems fundamentals, and observability.

EXPERIENCE
Senior Software Engineer - Contoso (2022-Present)
- Built event-driven microservices on Azure using .NET, Azure Service Bus, and Cosmos DB.
- Reduced p95 latency 45% by redesigning caching and async I/O patterns.
- Led on-call rotation improvements: dashboards, SLOs, runbooks.

Software Engineer - Fabrikam (2019-2022)
- Implemented payments API (C#, ASP.NET Core) with idempotency + audit logging.
- Collaborated with product on risk controls and fraud signals.

EDUCATION
B.S. Computer Science

SKILLS
C#, .NET, ASP.NET Core, Azure, SQL, Cosmos DB, Service Bus, Redis, CI/CD, Distributed Systems

PROJECTS
- Open-source: small library for resilient HTTP client policies.
```

---

## Step 5 - Create `Models/ResumeProfile.cs`

This is the strongly typed target for the JSON the agent returns.

```csharp
using System.Text.Json.Serialization;

namespace InterviewAssistant.Models;

public sealed class ResumeProfile
{
    [JsonPropertyName("candidateName")]   public string  CandidateName   { get; set; } = "";
    [JsonPropertyName("email")]           public string? Email           { get; set; }
    [JsonPropertyName("currentTitle")]    public string? CurrentTitle    { get; set; }
    [JsonPropertyName("yearsExperience")] public double? YearsExperience { get; set; }
    [JsonPropertyName("coreSkills")]      public List<string> CoreSkills      { get; set; } = new();
    [JsonPropertyName("roles")]           public List<string> Roles           { get; set; } = new();
    [JsonPropertyName("notableProjects")] public List<string> NotableProjects { get; set; } = new();
    [JsonPropertyName("redFlags")]        public List<string> RedFlags        { get; set; } = new();
}
```

Each property is annotated with `[JsonPropertyName]` so `System.Text.Json` maps the
camelCase JSON keys returned by the model to the PascalCase C# properties.

---

## Step 6 - Create `Agents/AgentPrompts.cs`

Centralises the system-instruction strings for each agent in the pipeline.

```csharp
namespace InterviewAssistant.Agents;

public static class AgentPrompts
{
    public const string ResumeIngestion = @"
You are a resume ingestion agent.

Goal:
- Extract a structured profile from the resume text.

Rules:
- Output MUST be valid JSON and MUST match the schema exactly.
- Do NOT wrap the JSON in markdown.
- If unknown, use null or empty list.

Schema:
{
  ""candidateName"": string,
  ""email"": string | null,
  ""currentTitle"": string | null,
  ""yearsExperience"": number | null,
  ""coreSkills"": string[],
  ""roles"": string[],
  ""notableProjects"": string[],
  ""redFlags"": string[]
}
";
}
```

The `""` double-escaping is required inside a C# verbatim string (`@"..."`) to produce a
literal `"` character in the prompt text.

---

## Step 7 - Create `Agents/JsonAgentRunner.cs`

A reusable utility that runs any `AIAgent`, receives its text output, and deserialises
it into `T`. It has no dependency on how the agent was constructed.

```csharp
using System.Text.Json;
using Microsoft.Agents.AI;

namespace InterviewAssistant.Agents;

public static class JsonAgentRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling        = JsonCommentHandling.Skip,
        AllowTrailingCommas        = true
    };

    public static async Task<(T Value, string Raw)> RunJsonAsync<T>(
        AIAgent agent,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var result = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var raw    = result.Text.Trim();

        try
        {
            var value = JsonSerializer.Deserialize<T>(raw, JsonOptions)
                        ?? throw new JsonException("Deserialized null");
            return (value, raw);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Agent returned non-JSON or schema mismatch. Raw:\n{raw}", ex);
        }
    }
}
```

---

## Step 8 - Write `Program.cs` with Inline Agent Creation

This is the central step. The agent is built in three explicit sub-steps instead of
being hidden behind a factory call.

### 8a - Read configuration from environment variables

```csharp
var endpoint   = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                 ?? throw new InvalidOperationException("Missing AZURE_OPENAI_ENDPOINT env var");

var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
                 ?? throw new InvalidOperationException("Missing AZURE_OPENAI_DEPLOYMENT env var");

var apiKey     = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
```

### 8b - Choose the authentication credential

```csharp
AzureOpenAIClient openAiClient = string.IsNullOrWhiteSpace(apiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    : new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
```

`AzureCliCredential` uses the token from an active `az login` session (local dev).
`AzureKeyCredential` is used when an API key is explicitly provided (CI/CD, production).

### 8c - Get the chat client and call `.AsAIAgent()`

```csharp
AIAgent ingestionAgent = openAiClient
    .GetChatClient(deployment)
    .AsAIAgent(instructions: AgentPrompts.ResumeIngestion, name: "ResumeIngestion");
```

- `GetChatClient()` is provided by the `Azure.AI.OpenAI` package.
- `.AsAIAgent()` is the extension method from `Microsoft.Agents.AI.OpenAI` that wraps
  the `ChatClient` inside the Agents Framework `AIAgent` abstraction.

### Complete `Program.cs`

```csharp
// Demo 1 - Single Agent: Resume Ingestion
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using InterviewAssistant.Agents;
using InterviewAssistant.Models;
using Microsoft.Agents.AI;

var resumePath = args.Length > 0 ? args[0] : Path.Combine("assets", "resumes", "jane_doe.txt");

if (!File.Exists(resumePath))
{
    Console.Error.WriteLine($"Resume not found: {resumePath}");
    return;
}

var resumeText = await File.ReadAllTextAsync(resumePath);

// 8a - Configuration
var endpoint   = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                 ?? throw new InvalidOperationException("Missing AZURE_OPENAI_ENDPOINT env var");

var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
                 ?? throw new InvalidOperationException("Missing AZURE_OPENAI_DEPLOYMENT env var");

var apiKey     = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

// 8b - Credential
AzureOpenAIClient openAiClient = string.IsNullOrWhiteSpace(apiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    : new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

// 8c - Agent
AIAgent ingestionAgent = openAiClient
    .GetChatClient(deployment)
    .AsAIAgent(instructions: AgentPrompts.ResumeIngestion, name: "ResumeIngestion");

Console.WriteLine("=== Demo 1: Single Agent ===\n");

var ingestPrompt = $"{AgentPrompts.ResumeIngestion}\n\nRESUME:\n{resumeText}";
var (profile, _) = await JsonAgentRunner.RunJsonAsync<ResumeProfile>(ingestionAgent, ingestPrompt);

Console.WriteLine($"Candidate : {profile.CandidateName}");
Console.WriteLine($"Experience: {profile.YearsExperience} years");
Console.WriteLine($"Skills    : {string.Join(", ", profile.CoreSkills.Take(8))}");
Console.WriteLine($"Red Flags : {string.Join(", ", profile.RedFlags)}");
```

---

## Step 9 - Build and Run

```pwsh
dotnet build
dotnet run
```

Expected output (values will vary based on model response):

```
=== Demo 1: Single Agent ===

Candidate : Jane Doe
Experience: 6 years
Skills    : C#, .NET, ASP.NET Core, Azure, SQL, Cosmos DB, Service Bus, Redis
Red Flags :
```

Pass a different resume as a command-line argument:

```pwsh
dotnet run -- path\to\other_resume.txt
```

---

## How Each Piece Connects

```
Program.cs
  |
  |-- reads env vars --------------------------------> AZURE_OPENAI_* variables
  |
  |-- new AzureOpenAIClient(uri, credential)          (Azure.AI.OpenAI)
  |       |
  |       +-- .GetChatClient(deployment)
  |                 |
  |                 +-- .AsAIAgent(instructions, name) (Microsoft.Agents.AI.OpenAI)
  |                           |
  |                           +-- AIAgent
  |                                 |
  |                                 +-- JsonAgentRunner.RunJsonAsync<ResumeProfile>()
  |                                           |
  |                                           |-- agent.RunAsync(prompt) --> Azure OpenAI API
  |                                           +-- JsonSerializer.Deserialize<ResumeProfile>()
  |
  +-- prints profile fields to console
```

---

## Files Created in This Guide

| File | Role |
|---|---|
| `InterviewAssistant.csproj` | SDK-style project, NuGet references |
| `assets/resumes/jane_doe.txt` | Sample input resume |
| `Models/ResumeProfile.cs` | Typed output model |
| `Agents/AgentPrompts.cs` | System instruction strings |
| `Agents/JsonAgentRunner.cs` | Generic agent runner with JSON deserialisation |
| `Program.cs` | Entry point - inline agent construction and pipeline execution |

> `AgentFactory.cs` is intentionally absent. Its three-step construction chain lives
> directly in `Program.cs`, keeping the data flow visible and self-contained.
