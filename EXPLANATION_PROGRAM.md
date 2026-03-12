# Program.cs — Detailed Explanation

This document explains the `Program.cs` file for **Demo 2: Multi-Agent Pipeline**, building directly on the single-agent baseline introduced in Demo 1.

---

## Overview

Demo 2 extends the single-agent resume ingestion into a full multi-agent interview assistant. Each agent owns exactly one responsibility; the typed output of one agent becomes the structured input to the next.

```
Resume Text
    │
    ▼
[Step 1] ResumeIngestion Agent   ──►  ResumeProfile (JSON)          ← Demo 1 baseline
                                           │
                                           ▼
[Step 2] SeniorityClassifier Agent ──►  SeniorityAssessment (JSON)  ← new in Demo 2
                                               │
                                               ▼
[Step 3] InterviewPlanner Agent    ──►  InterviewPlan (JSON)        ← new in Demo 2
                                            │
                              ┌─────────────┴─────────────┐
                           Approved                    Rejected
                              │                            │
                              │              [Planner revises with feedback]
                              └─────────────┬─────────────┘
                                            ▼
[Step 4] Evaluator Agent           ──►  EvaluationResult (JSON)     ← new in Demo 2
                                            │
                                            ▼
                                     Console output
```

---

## What Changed from Demo 1

| Area | Demo 1 | Demo 2 |
|---|---|---|
| CLI args | Positional `args[0]` for resume path | Named `--resume` and `--role` flags via `GetArg` helper |
| Agents | 1 — `ingestionAgent` | 4 — ingestion, seniority, planner, evaluator |
| Agent placement | Single declaration before the prompt | Each agent declared just before its own step |
| Output type | `ResumeProfile` | `ResumeProfile` → `SeniorityAssessment` → `InterviewPlan` → `EvaluationResult` |
| Human interaction | None | Approve / reject loop with iterative plan revision |
| `using` added | — | `System.Text` (for `StringBuilder`) |

---

## Step-by-Step Walkthrough

### 1. Named CLI arguments *(updated from Demo 1)*

**Demo 1** used a single positional argument for the resume path:

```csharp
var resumePath = args.Length > 0 ? args[0] : Path.Combine("assets", "resumes", "jane_doe.txt");
```

**Demo 2** introduces a `GetArg` helper and named flags, because the `InterviewPlanner` agent also needs a `--role` value:

```csharp
static string? GetArg(string[] args, string name)
{
    var idx = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0 && idx < args.Length - 1) return args[idx + 1];
    return null;
}

var role       = GetArg(args, "--role")   ?? "Software Engineer";
var resumePath = GetArg(args, "--resume") ?? Path.Combine("assets", "resumes", "jane_doe.txt");
```

The guard check and `File.ReadAllTextAsync` are identical to Demo 1 — only the argument parsing style changed.

---

### 2. Step 1 — Resume Ingestion *(Demo 1 baseline, unchanged)*

```csharp
AIAgent ingestionAgent = AgentFactory.CreateAzureOpenAIAgent("ResumeIngestion", AgentPrompts.ResumeIngestion);

Console.WriteLine("--- Step 1: Resume Ingestion ---\n");
var ingestPrompt = $"{AgentPrompts.ResumeIngestion}\n\nRESUME:\n{resumeText}";
var (profile, _) = await JsonAgentRunner.RunJsonAsync<ResumeProfile>(ingestionAgent, ingestPrompt);

Console.WriteLine($"Candidate : {profile.CandidateName}");
Console.WriteLine($"Experience: {profile.YearsExperience} years");
Console.WriteLine($"Skills    : {string.Join(", ", profile.CoreSkills.Take(8))}");
Console.WriteLine($"Red Flags : {string.Join(", ", profile.RedFlags)}");
```

This block is **structurally identical to Demo 1**. `ingestionAgent` is declared just before its step and produces the same `ResumeProfile`. The only visual change is the step header.

`profile` stays in scope for the rest of the file — every later agent receives it as input.

`AgentFactory.CreateAzureOpenAIAgent` wires up the Agent Framework to Azure OpenAI:

| Env var | Purpose |
|---|---|
| `AZURE_OPENAI_ENDPOINT` | Your Azure OpenAI resource URL |
| `AZURE_OPENAI_DEPLOYMENT` | Deployed model name (e.g. `gpt-4o-mini`) |
| `AZURE_OPENAI_API_KEY` | API key — **optional**, falls back to `AzureCliCredential` |

The internal call chain:

```
AzureOpenAIClient  ──►  GetChatClient(deployment)  ──►  AsAIAgent(instructions, name)
```

`JsonAgentRunner.RunJsonAsync<T>` calls `agent.RunAsync(prompt)`, trims the response, and deserialises the JSON into `T`. If the model returns malformed JSON, an `InvalidOperationException` is thrown with the raw response for debugging.

---

### 3. Step 2 — Seniority Classification *(new in Demo 2)*

```csharp
AIAgent seniorityAgent = AgentFactory.CreateAzureOpenAIAgent("SeniorityClassifier", AgentPrompts.SeniorityClassifier);

Console.WriteLine("\n--- Step 2: Seniority Classification ---\n");
var seniorityPrompt = $"{AgentPrompts.SeniorityClassifier}\n\nRESUME_PROFILE:\n{System.Text.Json.JsonSerializer.Serialize(profile)}";
var (seniority, _) = await JsonAgentRunner.RunJsonAsync<SeniorityAssessment>(seniorityAgent, seniorityPrompt);

Console.WriteLine($"Level     : {seniority.Level}  (confidence {seniority.Confidence:0.00})");
Console.WriteLine($"Rationale : {seniority.Rationale}");
```

Key points:

- `seniorityAgent` is declared **right before it is used** — the audience sees the new agent appear at the exact moment it enters the picture
- The output of Step 1 (`profile`) is serialised to JSON and injected as the input to Step 2 — this is the **type-safe hand-off** pattern
- `SeniorityClassifier` never sees the raw résumé text; it only knows about the already-structured `ResumeProfile` — **each agent only knows its job**

---

### 4. Step 3 — Interview Planning *(new in Demo 2)*

```csharp
AIAgent plannerAgent = AgentFactory.CreateAzureOpenAIAgent("InterviewPlanner", AgentPrompts.InterviewPlanner);

Console.WriteLine("\n--- Step 3: Interview Planning ---\n");
var planPrompt = new StringBuilder()
    .AppendLine(AgentPrompts.InterviewPlanner)
    .AppendLine()
    .AppendLine("ROLE:").AppendLine(role)
    .AppendLine()
    .AppendLine("RESUME_PROFILE:").AppendLine(System.Text.Json.JsonSerializer.Serialize(profile))
    .AppendLine()
    .AppendLine("SENIORITY:").AppendLine(System.Text.Json.JsonSerializer.Serialize(seniority))
    .ToString();

var (plan, _) = await JsonAgentRunner.RunJsonAsync<InterviewPlan>(plannerAgent, planPrompt);
```

The planner receives **three inputs** assembled with `StringBuilder`:

- `role` — the target job title from the CLI
- `profile` — the `ResumeProfile` from Step 1
- `seniority` — the `SeniorityAssessment` from Step 2

This is the **multi-context hand-off**: the planner benefits from both prior agents without either of those agents knowing about each other. `StringBuilder` is used here (instead of a single interpolated string) because the prompt has multiple clearly labelled sections that grow as more context is added.

---

### 5. Human-in-the-Loop Checkpoint *(new in Demo 2)*

```csharp
Console.Write("Approve this plan? (y/n): ");
var approved = (Console.ReadLine() ?? "").Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);

if (!approved)
{
    Console.Write("Give feedback in one sentence (e.g., 'more system design, fewer trivia'): ");
    var feedback = Console.ReadLine() ?? "";

    var revisePrompt = $@"Revise the InterviewPlan JSON below based on this feedback.
Feedback: {feedback}

Return ONLY valid InterviewPlan JSON.

{System.Text.Json.JsonSerializer.Serialize(plan)}";

    (plan, _) = await JsonAgentRunner.RunJsonAsync<InterviewPlan>(plannerAgent, revisePrompt);
}
```

This is the **Demo 2 showstopper** — a human approval gate between the planner and the evaluator.

What makes it powerful is its simplicity:

- The gate is a single `Console.ReadLine()` — the same concept maps directly to a Teams Adaptive Card or an API approval endpoint in production
- The revision prompt passes the planner its own **previous JSON output** plus the new feedback — the agent refines what it already produced rather than starting from scratch
- `plan` is reassigned in place so Step 4 always works with the latest approved (or revised) version

---

### 6. Step 4 — Evaluation *(new in Demo 2)*

```csharp
AIAgent evaluatorAgent = AgentFactory.CreateAzureOpenAIAgent("Evaluator", AgentPrompts.Evaluator);

Console.WriteLine("\n=== Step 4: Evaluation (simulate interview notes) ===\n");
Console.WriteLine("Type a few bullet notes about the candidate's performance, then enter an empty line:");

var notesSb = new StringBuilder();
while (true)
{
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line)) break;
    notesSb.AppendLine($"- {line}");
}

var notes = notesSb.Length == 0
    ? "- (no notes provided; evaluate based on resume + plan only)"
    : notesSb.ToString();
```

The evaluation step simulates post-interview notes typed by the interviewer. The `while (true)` / empty-line sentinel is the simplest possible multi-line console input pattern — no libraries needed.

The evaluator prompt assembles **all prior artifacts**:

```csharp
var evalPrompt = new StringBuilder()
    .AppendLine(AgentPrompts.Evaluator)
    .AppendLine()
    .AppendLine("RESUME_PROFILE:").AppendLine(System.Text.Json.JsonSerializer.Serialize(profile))
    .AppendLine()
    .AppendLine("INTERVIEW_PLAN:").AppendLine(System.Text.Json.JsonSerializer.Serialize(plan))
    .AppendLine()
    .AppendLine("INTERVIEW_NOTES:").AppendLine(notes)
    .ToString();

var (evaluation, _) = await JsonAgentRunner.RunJsonAsync<EvaluationResult>(evaluatorAgent, evalPrompt);
```

The evaluator is the only agent that sees the complete picture: résumé, plan, and live notes. This demonstrates how typed hand-offs **accumulate context** across a pipeline without any single agent being aware of the others.

---

## Key Types

### `ResumeProfile` *(Demo 1, unchanged)*

Defined in `Models/ResumeProfile.cs`:

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

### `SeniorityAssessment` *(new in Demo 2)*

Defined in `Models/SeniorityAssessment.cs`:

| Property | Type | Description |
|---|---|---|
| `Level` | `string` | `Junior` / `Mid` / `Senior` / `Staff+` |
| `Confidence` | `double` | 0.0 – 1.0 confidence score |
| `Rationale` | `string` | One-sentence justification |

### `InterviewPlan` *(new in Demo 2)*

Defined in `Models/InterviewPlan.cs`:

| Property | Type | Description |
|---|---|---|
| `Role` | `string` | Target job title |
| `Level` | `string` | Seniority level |
| `Summary` | `string` | Short plan overview |
| `Rounds` | `List<InterviewRound>` | Interview rounds — name, duration, and questions |
| `Rubric` | `List<RubricItem>` | Evaluation dimensions and signals |

### `EvaluationResult` *(new in Demo 2)*

Defined in `Models/EvaluationResult.cs`:

| Property | Type | Description |
|---|---|---|
| `OverallScore` | `int` | 1 – 10 |
| `Recommendation` | `string` | `Hire` / `Lean Hire` / `Lean No` / `No Hire` |
| `Summary` | `string` | Narrative summary |
| `Strengths` | `List<string>` | Positive signals |
| `Risks` | `List<string>` | Concerns or gaps |
| `FollowUps` | `List<string>` | Suggested follow-up questions |

### `AgentPrompts` *(extended in Demo 2)*

Defined in `Agents/AgentPrompts.cs` — four `const string` system prompts, one per agent. Each prompt embeds the exact JSON schema the model must produce, keeping the output always parseable by `JsonAgentRunner`.

---

## Data Flow

```
--resume / --role (CLI args)
         │
         ▼
  File.ReadAllTextAsync
         │
         ▼  ──────────────────────────────────────────  Step 1 (Demo 1 baseline)
  ingestionAgent
  JsonAgentRunner.RunJsonAsync<ResumeProfile>
         │
         ▼  ──────────────────────────────────────────  Step 2 (new)
  seniorityAgent
  JsonAgentRunner.RunJsonAsync<SeniorityAssessment>
  (input: ResumeProfile JSON)
         │
         ▼  ──────────────────────────────────────────  Step 3 (new)
  plannerAgent
  JsonAgentRunner.RunJsonAsync<InterviewPlan>
  (input: role + ResumeProfile + SeniorityAssessment)
         │
         ▼  ──────────────────────────────────────────  Human-in-the-Loop (new)
  Approve? (y/n)
    ├── yes → continue
    └── no  → feedback → plannerAgent revises plan → loop back
         │
         ▼  ──────────────────────────────────────────  Step 4 (new)
  evaluatorAgent
  JsonAgentRunner.RunJsonAsync<EvaluationResult>
  (input: ResumeProfile + InterviewPlan + interview notes)
         │
         ▼
  Console output (score, recommendation, strengths, risks, follow-ups)
```

---

## Running the demo

From the repo root:

```bash
cd src/InterviewAssistant
dotnet run -- --resume ../../assets/resumes/jane_doe.txt --role "Software Engineer"
```

Expected console flow:

```
=== Demo 2: Multi-Agent Pipeline ===

Role  : Software Engineer
Resume: ../../assets/resumes/jane_doe.txt

--- Step 1: Resume Ingestion ---

Candidate : Jane Doe
Experience: 6 years
Skills    : C#, .NET, Azure, Azure Service Bus, Cosmos DB, ...
Red Flags :

--- Step 2: Seniority Classification ---

Level     : Senior  (confidence 0.85)
Rationale : 6 years of relevant experience with strong Azure and distributed systems background.

--- Step 3: Interview Planning ---

=== Draft Interview Plan ===

Role: Software Engineer | Level: Senior

[plan summary printed here]

- Experience Deep Dive (15 min)
  • ...
- System Design (20 min)
  • ...
- Values & Role Fit (10 min)
  • ...

Approve this plan? (y/n): n
Give feedback in one sentence (e.g., 'more system design, fewer trivia'): more system design questions

=== Revised Plan ===
{ ... updated InterviewPlan JSON ... }

=== Step 4: Evaluation (simulate interview notes) ===

Type a few bullet notes about the candidate's performance, then enter an empty line:
> Strong system design answers
> Struggled with async patterns
>

=== Result ===

Score          : 7/10
Recommendation : Lean Hire

[summary, strengths, risks, follow-ups printed here]
```

---

## Extension points (Demo 3)

Demo 2's sequential `await` chain will be replaced by a **declarative workflow graph** in Demo 3:

```csharp
// Demo 3 — three lines replace the manual step-by-step chaining
var workflow = new WorkflowBuilder(ingestionAgent)
    .AddEdge(ingestionAgent, seniorityAgent)
    .AddEdge(seniorityAgent, plannerAgent)
    .Build();
```

Key improvements that workflow brings:

- **Streaming** — tokens from each agent stream to the terminal as they arrive via `InProcessExecution.StreamAsync`
- **Observability** — per-executor output is captured automatically without manual `StringBuilder` plumbing
- **Composability** — branching edges, retry policies, and human-in-the-loop gates are composable on top of the same graph

---

## Repo layout

```
src/InterviewAssistant/
  Program.cs               — CLI entrypoint (this file)
  Agents/
    AgentFactory.cs        — creates an AIAgent backed by Azure OpenAI
    AgentPrompts.cs        — system prompts for all four agents
    JsonAgentRunner.cs     — runs an agent and deserialises JSON output
  Models/
    ResumeProfile.cs       — POCO: résumé structured data           (Demo 1)
    SeniorityAssessment.cs — POCO: level + confidence + rationale   (Demo 2)
    InterviewPlan.cs       — POCO: rounds, questions, rubric        (Demo 2)
    EvaluationResult.cs    — POCO: score, recommendation, signals   (Demo 2)
assets/
  resumes/
    jane_doe.txt           — sample résumé used in the demo
```

---

*Based on the .NET 8 / C# 12 implementation using Microsoft Agent Framework — branch `demo/2-multi-agent`.*