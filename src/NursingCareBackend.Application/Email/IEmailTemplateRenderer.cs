namespace NursingCareBackend.Application.Email;

/// <summary>
/// Loads an email template by name, substitutes placeholders, and converts it to HTML.
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders the named template by replacing each placeholder key with the corresponding value
    /// and converting the resulting markdown to HTML.
    /// </summary>
    /// <param name="templateName">
    /// File name without path, e.g. <c>daily-admin-summary</c>.
    /// The implementation resolves the physical path.
    /// </param>
    /// <param name="variables">Placeholder name → replacement value map. Keys include braces, e.g. <c>{{Fecha}}</c>.</param>
    /// <returns>An HTML string ready to send via <see cref="IEmailService"/>.</returns>
    string Render(string templateName, IReadOnlyDictionary<string, string> variables);
}
