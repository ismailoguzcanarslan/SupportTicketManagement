
using SupportTicket.EntityGenerator.Configuration;
using System.Diagnostics;

namespace SupportTicket.EntityGenerator.Generation.CSharp;

public sealed class PacModelBuilderGenerator
{
    public string BuildCommand(GenerationSettings settings, IReadOnlyList<string> tables, string settingsDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);

        if (tables.Count == 0)
            throw new ArgumentException("Üretilecek tablo listesi boş olamaz.", nameof(tables));

        var entityNames = string.Join(";", tables);
        var outputDirectory = Path.GetFullPath(settings.Output.CSharp, settingsDirectory);
        var nameSpace = settings.Output.CSharpNamespace;

        return $"pac modelbuilder build --outdirectory \"{outputDirectory}\" --entitynamesfilter \"{entityNames}\" --namespace \"{nameSpace}\" --emitfieldsclasses --serviceContextName \"SupportTicketContext\"";
    }


    public async Task<int> ExecuteAsync(GenerationSettings settings, IReadOnlyList<string> tables, string settingsDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);

        if (tables.Count == 0)
            throw new ArgumentException("Üretilecek tablo listesi boş olamaz.", nameof(tables));

        var outputDirectory = Path.GetFullPath(settings.Output.CSharp, settingsDirectory);
        var entityNames = string.Join(";", tables);

        var startInfo = new ProcessStartInfo
        {
            FileName = "pac",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("modelbuilder");
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add("--outdirectory");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add("--entitynamesfilter");
        startInfo.ArgumentList.Add(entityNames);
        startInfo.ArgumentList.Add("--namespace");
        startInfo.ArgumentList.Add(settings.Output.CSharpNamespace);
        startInfo.ArgumentList.Add("--emitfieldsclasses");
        startInfo.ArgumentList.Add("--serviceContextName");
        startInfo.ArgumentList.Add("SupportTicketContext");

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (!string.IsNullOrWhiteSpace(output))
            System.Console.WriteLine(output);

        if (!string.IsNullOrWhiteSpace(error))
            System.Console.Error.WriteLine(error);

        return process.ExitCode;
    }

}
