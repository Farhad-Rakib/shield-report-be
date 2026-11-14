namespace ShieldReport.Domain.Enums;

// Stored as a byte: append new values, never reorder.
public enum LlmProvider
{
    OpenRouter,
    Gemini,
    OpenAi,
    Anthropic,
    // Any other endpoint speaking the OpenAI chat-completions format (Azure OpenAI proxy, Groq,
    // Together, a self-hosted vLLM/Ollama gateway, ...). BaseUrl is required.
    OpenAiCompatible
}
