// Demo 2 — Multi-Agent Pipeline + Human-in-the-Loop
using System.Text;
using InterviewAssistant.Agents;
using InterviewAssistant.Models;
using Microsoft.Agents.AI;

static string? GetArg(string[] args, string name)
{
    var idx = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0 && idx < args.Length - 1) return args[idx + 1];
    return null;
}

var role = GetArg(args, "--role") ?? "Software Engineer";
var resumePath = GetArg(args, "--resume") ?? Path.Combine("assets", "resumes", "jane_doe.txt");

if (!File.Exists(resumePath))
{
    Console.Error.WriteLine($"Resume not found: {resumePath}");
    return;
}

var resumeText = await File.ReadAllTextAsync(resumePath);

Console.WriteLine("\n=== Demo 2: Multi-Agent Pipeline ===\n");
Console.WriteLine($"Role  : {role}");
Console.WriteLine($"Resume: {resumePath}\n");

// ---- Step 1: Resume Ingestion (Demo 1 baseline) ----
AIAgent ingestionAgent = AgentFactory.CreateAzureOpenAIAgent("ResumeIngestion", AgentPrompts.ResumeIngestion);

Console.WriteLine("--- Step 1: Resume Ingestion ---\n");
var ingestPrompt = $"{AgentPrompts.ResumeIngestion}\n\nRESUME:\n{resumeText}";
var (profile, _) = await JsonAgentRunner.RunJsonAsync<ResumeProfile>(ingestionAgent, ingestPrompt);

Console.WriteLine($"Candidate : {profile.CandidateName}");
Console.WriteLine($"Experience: {profile.YearsExperience} years");
Console.WriteLine($"Skills    : {string.Join(", ", profile.CoreSkills.Take(8))}");
Console.WriteLine($"Red Flags : {string.Join(", ", profile.RedFlags)}");

// ---- Step 2: Seniority Classification ----
AIAgent seniorityAgent = AgentFactory.CreateAzureOpenAIAgent("SeniorityClassifier", AgentPrompts.SeniorityClassifier);

Console.WriteLine("\n--- Step 2: Seniority Classification ---\n");
var seniorityPrompt = $"{AgentPrompts.SeniorityClassifier}\n\nRESUME_PROFILE:\n{System.Text.Json.JsonSerializer.Serialize(profile)}";
var (seniority, _) = await JsonAgentRunner.RunJsonAsync<SeniorityAssessment>(seniorityAgent, seniorityPrompt);

Console.WriteLine($"Level     : {seniority.Level}  (confidence {seniority.Confidence:0.00})");
Console.WriteLine($"Rationale : {seniority.Rationale}");

// ---- Step 3: Interview Planning + Human-in-the-Loop ----
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

// ---- Human-in-the-Loop Checkpoint ----
Console.WriteLine("\n=== Draft Interview Plan ===\n");
Console.WriteLine($"Role: {plan.Role} | Level: {plan.Level}\n");
Console.WriteLine(plan.Summary);
Console.WriteLine();

foreach (var round in plan.Rounds)
{
    Console.WriteLine($"- {round.Name} ({round.DurationMinutes} min)");
    foreach (var q in round.Questions.Take(4)) Console.WriteLine($"  • {q}");
    if (round.Questions.Count > 4) Console.WriteLine("  • ...");
    Console.WriteLine();
}

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
    Console.WriteLine("\n=== Revised Plan ===\n");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(plan, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
}

// ---- Step 4: Evaluation ----
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

Console.WriteLine("\n=== Result ===\n");
Console.WriteLine($"Score          : {evaluation.OverallScore}/10");
Console.WriteLine($"Recommendation : {evaluation.Recommendation}\n");
Console.WriteLine(evaluation.Summary);

Console.WriteLine("\nStrengths:");
foreach (var s in evaluation.Strengths) Console.WriteLine($"  • {s}");

Console.WriteLine("\nRisks:");
foreach (var r in evaluation.Risks) Console.WriteLine($"  • {r}");

Console.WriteLine("\nFollow-ups:");
foreach (var f in evaluation.FollowUps) Console.WriteLine($"  • {f}");
