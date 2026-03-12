// Demo 1 — Single Agent: Resume Ingestion
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

// WE CREATE A SINGLE AGENT, which will be responsible for parsing the resume and extracting key information
// such as candidate name, years of experience, core skills, and potential red flags. The agent is created using the AgentFactory,
// which sets up an Azure OpenAI Agent with specific instructions defined in AgentPrompts.ResumeIngestion.
AIAgent ingestionAgent = AgentFactory.CreateAzureOpenAIAgent("ResumeIngestion", AgentPrompts.ResumeIngestion);

Console.WriteLine("=== Demo 1: Single Agent ===\n");

var ingestPrompt = $"{AgentPrompts.ResumeIngestion}\n\nRESUME:\n{resumeText}";
var (profile, _) = await JsonAgentRunner.RunJsonAsync<ResumeProfile>(ingestionAgent, ingestPrompt);

Console.WriteLine($"Candidate : {profile.CandidateName}");
Console.WriteLine($"Experience: {profile.YearsExperience} years");
Console.WriteLine($"Skills    : {string.Join(", ", profile.CoreSkills.Take(8))}");
Console.WriteLine($"Red Flags : {string.Join(", ", profile.RedFlags)}");
