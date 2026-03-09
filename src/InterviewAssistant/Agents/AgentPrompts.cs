namespace InterviewAssistant.Agents;

public static class AgentPrompts
{
    // Demo 1 — The only prompt needed for a single-agent pipeline.
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

    // TODO Demo 2 — Add SeniorityClassifier, InterviewPlanner, and Evaluator prompts here.
}
