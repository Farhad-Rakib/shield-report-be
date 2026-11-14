using System.Text;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Infrastructure.Llm;

// Claude via the official Anthropic SDK. The API key differs per saved provider configuration, so
// a client is built per call (calls are rare: one per finding explanation or report draft).
//
// Temperature is deliberately not sent: current Claude models reject sampling parameters, and
// the prompts already pin the output to strict JSON. Depth is governed by the model's default
// effort. Refusal fallbacks are enabled so a policy decline is re-served by another model inside
// the same call instead of failing the job.
public sealed class AnthropicLlmClient : ILlmClient
{
    public const string DefaultModel = "claude-opus-5-5";

    public async Task<string> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var client = string.IsNullOrWhiteSpace(request.BaseUrl)
            ? new AnthropicClient { ApiKey = request.ApiKey, Timeout = TimeSpan.FromSeconds(60), MaxRetries = 1 }
            : new AnthropicClient { ApiKey = request.ApiKey, Timeout = TimeSpan.FromSeconds(60), MaxRetries = 1, BaseUrl = request.BaseUrl.TrimEnd('/') };

        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model,
            MaxTokens = Math.Max(request.MaxOutputTokens, 1024),
            Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
            Fallbacks = new Default(),
            Messages = [new BetaUserMessageParam { Content = request.Prompt }],
        }, cancellationToken);

        if (response.StopReason == "refusal")
        {
            throw new InvalidOperationException("The model declined to answer this request.");
        }

        var text = new StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out BetaTextBlock? textBlock))
            {
                text.Append(textBlock.Text);
            }
        }

        if (response.StopReason == "max_tokens" && text.Length == 0)
        {
            throw new InvalidOperationException("The model ran out of output tokens before answering. Raise Max output tokens for this provider.");
        }

        return text.ToString();
    }
}
