namespace Preesta.AppConfig
{
    /// <summary>
    /// Validated Jira connection parameters. By the time a
    /// <see cref="JiraConfig"/> exists, <see cref="RootUri"/> is non-null and
    /// either <see cref="ApiToken"/> is set (Bearer auth) or both
    /// <see cref="UserName"/> and <see cref="Password"/> are set (Basic auth)
    /// — the loader rejects every other shape.
    /// </summary>
    internal sealed record JiraConfig(
        string RootUri,
        string? ApiToken,
        string? UserName,
        string? Password,
        int MaxResults);
}
