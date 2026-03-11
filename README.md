# Microsoft Agent Framework — AI Interview Assistant (demo 1)

This repo is a **hands-on** tech talk showcasing **Microsoft Agent Framework** AI Agents — LLM-driven agents that produce structured output from unstructured text.

> Branch **`demo/1-single-agent`** — a single agent that ingests a resume and returns structured JSON.

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
dotnet run ../../assets/resumes/jane_doe.txt
```

The resume path argument is optional — when omitted it defaults to `assets/resumes/jane_doe.txt` (resolved relative to the project directory).

### Expected output

```
=== Demo 1: Single Agent ===

Candidate : Jane Doe
Experience: 6 years
Skills    : C#, .NET, Azure, Azure Service Bus, Cosmos DB, ...
Red Flags :
```

---

## 4) What this demo shows

- A resume text file is passed to an **AI Agent** backed by Azure OpenAI
- The agent returns **structured JSON** (`ResumeProfile`) parsed directly from the model response
- Auth supports both **Azure CLI / RBAC** (`az login`) and an explicit **API key**

---

## Repo layout

```
src/InterviewAssistant/
  Program.cs            — CLI entrypoint (reads resume path from args)
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
