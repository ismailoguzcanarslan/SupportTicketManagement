namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>Console input/output, abstracted so the menu and confirmations are testable.</summary>
public interface IUserPrompt
{
    void WriteLine(string text = "");

    /// <summary>Returns null when input has ended (e.g. redirected stdin), so callers never block forever.</summary>
    string? ReadLine(string prompt);
}

public sealed class ConsoleUserPrompt : IUserPrompt
{
    public void WriteLine(string text = "") => Console.WriteLine(text);

    public string? ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }
}

public static class UserPromptExtensions
{
    /// <summary>
    /// Asks the user to type the environment name to approve a change. Anything else (including
    /// end of input) declines.
    /// </summary>
    public static bool ConfirmByTypingEnvironment(this IUserPrompt prompt, string question, string environment)
    {
        var answer = prompt.ReadLine($"{question}{Environment.NewLine}Type {environment} to confirm, or press Enter to cancel: ");
        return string.Equals(answer?.Trim(), environment, StringComparison.Ordinal);
    }
}
