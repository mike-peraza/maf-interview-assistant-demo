# Microsoft Agent Framework — AI Interview Assistant (demo 2)

This repo is a **hands-on** tech talk showcasing **Microsoft Agent Framework** AI Agents — LLM-driven agents that produce structured output from unstructured text.

> Branch **`demo/2-multi-agent`** — four specialised agents chained together, with a human-in-the-loop approval gate between planning and evaluation.

---

## 0) Prereqs

- **.NET 8 SDK+**
- **Azure OpenAI** endpoint + a deployed chat model (e.g., `gpt-4o-mini`)
- **Azure CLI** authenticated via `az login` **OR** an API key

---

## 1) Setup (packages)

From `src/InterviewAssistant`:

```bash
# Core agent packages (from the Learn quick-start)
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
```

No new packages are needed beyond Demo 1 — the multi-agent pipeline runs on the same dependencies.

---

## 2) Configure environment

Set these environment variables before running:

```bash
# Required
$env:AZURE_OPENAI_ENDPOINT   = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4o-mini"

# Optional — omit to use Azure CLI / RBAC auth (az login)
$env:AZURE_OPENAI_API_KEY    = "your-api-key"
```

---

## 3) Run

From the **repo root**:

```bash
cd src/InterviewAssistant
dotnet run -- --resume ../../assets/resumes/jane_doe.txt --role "Software Engineer"
```

Both arguments are optional — when omitted they default to `assets/resumes/jane_doe.txt` and `Software Engineer`.

### Expected output

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
Rationale : 6 years of relevant experience with strong Azure background.

--- Step 3: Interview Planning ---

=== Draft Interview Plan ===

Role: Software Engineer | Level: Senior
...

Approve this plan? (y/n): n
Give feedback in one sentence: more system design questions

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
...
```

---

## 4) What this demo shows

- **Multi-agent pipeline** — four specialised agents, each with a single responsibility, chained sequentially
- **Type-safe hand-offs** — the typed JSON output of each agent becomes the structured input to the next (`ResumeProfile` → `SeniorityAssessment` → `InterviewPlan` → `EvaluationResult`)
- **Human-in-the-loop** — a `Console.ReadLine()` approval gate between the planner and the evaluator; demonstrates how the same pattern applies to Teams cards or API approval endpoints in production
- **Iterative revision** — when the plan is rejected the planner receives its own previous JSON plus feedback and refines it in place
- **Auth** — supports both **Azure CLI / RBAC** (`az login`) and an explicit **API key**

---

## Repo layout

```
src/InterviewAssistant/
  Program.cs               — CLI entrypoint (--resume and --role flags)
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