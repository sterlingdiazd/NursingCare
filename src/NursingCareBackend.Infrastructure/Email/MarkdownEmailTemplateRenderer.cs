using Markdig;
using NursingCareBackend.Application.Email;

namespace NursingCareBackend.Infrastructure.Email;

/// <summary>
/// Loads an email template from the embedded resource bundle, substitutes placeholders,
/// and converts the resulting Markdown to HTML using Markdig.
/// </summary>
public sealed class MarkdownEmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string Render(string templateName, IReadOnlyDictionary<string, string> variables)
    {
        var markdown = LoadTemplate(templateName);
        var substituted = Substitute(markdown, variables);
        return Markdown.ToHtml(substituted, Pipeline);
    }

    private static string LoadTemplate(string templateName)
    {
        // Normalize: strip directory component and extension if the caller passed them.
        var stem = Path.GetFileNameWithoutExtension(templateName);
        var resourceName = $"NursingCareBackend.Infrastructure.Email.Templates.{stem}.md";

        var assembly = typeof(MarkdownEmailTemplateRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Email template '{stem}.md' was not found as embedded resource '{resourceName}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Substitute(string template, IReadOnlyDictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
        {
            template = template.Replace(key, value, StringComparison.Ordinal);
        }

        return template;
    }
}
