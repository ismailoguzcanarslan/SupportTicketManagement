
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.DependencyInjection;
using SupportTicket.EntityGenerator.Configuration;
using SupportTicket.EntityGenerator.Console;
using SupportTicket.Tools.Dataverse;
using SupportTicket.EntityGenerator.Generation.CSharp;
using SupportTicket.EntityGenerator.Generation.TypeScript;
using SupportTicket.EntityGenerator.Services;

var settingsPath = Path.Combine(System.AppContext.BaseDirectory, "generator.settings.json");
var projectDirectory = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, "..", "..", ".."));
var generationSettings = GenerationSettingsLoader.Load(settingsPath);

var mode = GenerationMenu.Show();

if (mode == GenerationMode.Exit)
    return;

try
{
    var connectionSettings = DataverseConnectionSettingsLoader.Load();
    var connectionService = new DataverseConnectionService();

    System.Console.WriteLine("Connecting to Dataverse...");

    using var client = connectionService.Connect(connectionSettings);

    var services = new ServiceCollection();

    services.AddSingleton(client);
    services.AddSingleton<IDataverseMetadataService, DataverseMetadataService>();
    services.AddSingleton<TableSelectionService>();
    services.AddSingleton<PacModelBuilderGenerator>();
    services.AddTransient<TypeScriptGenerator>();

    using var serviceProvider = services.BuildServiceProvider();

    var response = (WhoAmIResponse)client.Execute(new WhoAmIRequest());

    System.Console.WriteLine($"Dataverse connection successful. User ID: {response.UserId}");

    if (mode == GenerationMode.Backend || mode == GenerationMode.Both)
    {
        var tableSelectionService = serviceProvider.GetRequiredService<TableSelectionService>();
        var backendTables = await tableSelectionService.GetBackendTablesAsync(generationSettings);

        var metadataService = serviceProvider.GetRequiredService<IDataverseMetadataService>();
        var usedGlobalChoices = await metadataService.GetUsedGlobalChoicesAsync(backendTables);

        System.Console.WriteLine($"Kullanılan Global Choice sayısı: {usedGlobalChoices.Count}");

        foreach (var choice in usedGlobalChoices)
            System.Console.WriteLine($" - {choice.Name}");

        System.Console.WriteLine();
        System.Console.WriteLine($"Backend Generation Entity Count: {backendTables.Count}");

        foreach (var table in backendTables)
            System.Console.WriteLine($"- {table}");

        var pacGenerator = serviceProvider.GetRequiredService<PacModelBuilderGenerator>();
        var pacCommand = pacGenerator.BuildCommand(generationSettings, backendTables, projectDirectory);

        System.Console.WriteLine();
        System.Console.WriteLine("PAC ModelBuilder Command:");
        System.Console.WriteLine(pacCommand);

        System.Console.WriteLine("Starting PAC ModelBuilder...");

        var exitCode = await pacGenerator.ExecuteAsync(generationSettings, backendTables, projectDirectory);

        if (exitCode != 0)
            throw new InvalidOperationException($"PAC ModelBuilder failed. Exit code: {exitCode}");

        System.Console.WriteLine("PAC ModelBuilder completed successfully.");
    }

    if (mode == GenerationMode.Frontend || mode == GenerationMode.Both)
    {
        var frontendSettings = generationSettings.Generation.Frontend;

        if (frontendSettings.Enabled)
        {
            var outputDirectory = Path.GetFullPath(
                Path.Combine(projectDirectory, generationSettings.Output.TypeScript));

            var typeScriptGenerator = serviceProvider.GetRequiredService<TypeScriptGenerator>();

            System.Console.WriteLine();
            System.Console.WriteLine($"Frontend Generation Entity Count: {frontendSettings.Tables.Count}");

            await typeScriptGenerator.GenerateAsync(frontendSettings.Tables, outputDirectory);

            System.Console.WriteLine("Frontend TypeScript generation completed successfully.");
        }
        else
        {
            System.Console.WriteLine("Frontend TypeScript generation is disabled.");
        }
    }
}
catch (Exception ex)
{
    System.Console.Error.WriteLine($"Entity Generator failed: {ex.Message}");
    Environment.ExitCode = 1;
}
