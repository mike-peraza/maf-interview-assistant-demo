# Program.cs — Detailed Explanation

This document explains the `Program.cs` file for **Demo 1: Single Agent**, the entry point of the AI Interview Assistant built with the Microsoft Agent Framework.

---

## Overview

The application is a minimal .NET 8 console app that feeds a résumé text file to a single AI Agent and prints a structured profile extracted by the model.

```
Resume text  ──►  ResumeIngestion Agent  ──►  ResumeProfile (JSON)  ──►  Console output
```

---

## Step-by-Step Walkthrough

### 1. Resolve the resume path

```csharp
var resumePath = args.Length > 0 ? args[0] : Path.Combine("assets", "resumes", "jane_doe.txt");
```

The first CLI argument is treated as the path to the résumé file.  
If no argument is supplied, the app falls back to the bundled sample at `assets/resumes/jane_doe.txt` (relative to the project directory).

```csharp
if (!File.Exists(resumePath))
{
    Console.Error.WriteLine($"Resume not found: {resumePath}");
    return;
}

var resumeText = await File.ReadAllTextAsync(resumePath);
```

The file is read as plain text before any agent is involved — there is no streaming or chunking at this stage.

---

### 2. Create the agent

```csharp
AIAgent ingestionAgent = AgentFactory.CreateAzureOpenAIAgent("ResumeIngestion", AgentPrompts.ResumeIngestion);
```

`AgentFactory.CreateAzureOpenAIAgent` wires up the Agent Framework to Azure OpenAI:

| Env var | Purpose |
|---|---|
| `AZURE_OPENAI_ENDPOINT` | Your Azure OpenAI resource URL |
| `AZURE_OPENAI_DEPLOYMENT` | Deployed model name (e.g. `gpt-4o-mini`) |
| `AZURE_OPENAI_API_KEY` | API key — **optional**, falls back to `AzureCliCredential` |

The call chain inside the factory is:

```
AzureOpenAIClient  ──►  GetChatClient(deployment)  ──►  AsAIAgent(instructions, name)
```

`AsAIAgent` is an extension provided by `Microsoft.Agents.AI.OpenAI` that wraps the chat client as a first-class `AIAgent`.

---

### 3. Build the prompt

```csharp
var ingestPrompt = $"{AgentPrompts.ResumeIngestion}\n\nRESUME:\n{resumeText}";
```

The prompt combines two parts:

- **`AgentPrompts.ResumeIngestion`** — the system-level instructions that tell the model what schema to return and how to behave (no markdown wrapping, use `null` for unknown fields, etc.)
- **The raw résumé text** — appended after a separator so the model has the candidate's actual content to parse

---

### 4. Run the agent and deserialise the response

```csharp
var (profile, _) = await JsonAgentRunner.RunJsonAsync<ResumeProfile>(ingestionAgent, ingestPrompt);
```

`JsonAgentRunner.RunJsonAsync<T>` does three things:

1. Calls `agent.RunAsync(prompt)` — sends the prompt to the model and awaits the response
2. Trims leading/trailing whitespace from the raw text response
3. Deserialises the JSON into `T` using `System.Text.Json` with case-insensitive property matching

If the model returns malformed JSON or a schema mismatch, an `InvalidOperationException` is thrown with the raw response included for debugging.

---

### 5. Print the result

```csharp
Console.WriteLine($"Candidate : {profile.CandidateName}");
Console.WriteLine($"Experience: {profile.YearsExperience} years");
Console.WriteLine($"Skills    : {string.Join(", ", profile.CoreSkills.Take(8))}");
Console.WriteLine($"Red Flags : {string.Join(", ", profile.RedFlags)}");
```

The strongly-typed `ResumeProfile` is printed to the console. Skills are capped at 8 entries to keep output readable.

---

## Key Types

### `ResumeProfile` model

Defined in `Models/ResumeProfile.cs` — the target schema the agent is instructed to produce:

| Property | Type | Description |
|---|---|---|
| `CandidateName` | `string` | Full name |
| `Email` | `string?` | Contact email |
| `CurrentTitle` | `string?` | Most recent job title |
| `YearsExperience` | `double?` | Total years of experience |
| `CoreSkills` | `List<string>` | Technical skills |
| `Roles` | `List<string>` | Previous job titles |
| `NotableProjects` | `List<string>` | Stand-out projects |
| `RedFlags` | `List<string>` | Concerns (gaps, inconsistencies, etc.) |

### `AgentPrompts.ResumeIngestion`

Defined in `Agents/AgentPrompts.cs` — a `const string` containing the system prompt.  
It explicitly provides the JSON schema to the model so the output is always parseable.

---

## Data Flow

```
args[0] (or default path)
         │
         ▼
  File.ReadAllTextAsync
         │
         ▼
  ingestPrompt = system prompt + resume text
         │
         ▼
  AgentFactory.CreateAzureOpenAIAgent
  (AzureOpenAIClient → ChatClient → AIAgent)
         │
         ▼
  JsonAgentRunner.RunJsonAsync<ResumeProfile>
  (agent.RunAsync → trim → JSON deserialise)
         │
         ▼
  Console output (name, experience, skills, red flags)
```

---

## Running the demo

From the repo root:

```bash
cd src/InterviewAssistant
dotnet run ../../assets/resumes/jane_doe.txt
```

Expected output:

```
=== Demo 1: Single Agent ===

Candidate : Jane Doe
Experience: 6 years
Skills    : C#, .NET, Azure, Azure Service Bus, Cosmos DB, ...
Red Flags :
```

---

## Extension points (future demos)

The `AgentPrompts.cs` file already contains a `TODO` comment marking where additional agents will be added in later branches:

```csharp
// TODO Demo 2 — Add SeniorityClassifier, InterviewPlanner, and Evaluator prompts here.
```

The next demos will introduce:

- **Multi-agent pipelines** — chain the output of one agent as input to the next
- **Workflow orchestration** — explicit graphs with checkpoints and human-in-the-loop approval

---

## Repo layout

```
src/InterviewAssistant/
  Program.cs            — CLI entrypoint (this file)
  Agents/
    AgentFactory.cs     — creates an AIAgent backed by Azure OpenAI
    AgentPrompts.cs     — system prompt for resume ingestion
    JsonAgentRunner.cs  — runs the agent and deserialises JSON output
  Models/
    ResumeProfile.cs    — POCO for structured resume output
assets/
  resumes/
    jane_doe.txt        — sample résumé used in the demo
```

---

*Based on the .NET 8 / C# 12 implementation using Microsoft Agent Framework — branch `demo/1-single-agent`.*
