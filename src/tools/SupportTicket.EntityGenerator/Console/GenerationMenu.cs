
namespace SupportTicket.EntityGenerator.Console;

public static class GenerationMenu
{
    public static GenerationMode Show()
    {
        while (true)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("====================================");
            System.Console.WriteLine("   SupportTicket Entity Generator");
            System.Console.WriteLine("====================================");
            System.Console.WriteLine("[1] Backend  - C# Early Bound");
            System.Console.WriteLine("[2] Frontend - TypeScript");
            System.Console.WriteLine("[3] Both     - Backend + Frontend");
            System.Console.WriteLine("[0] Exit");
            System.Console.WriteLine();

            System.Console.Write("Enter your choice: ");

            var input = System.Console.ReadLine();

            // End of input (redirected or closed stdin): exit instead of looping forever.
            if (input is null)
            {
                return GenerationMode.Exit;
            }

            if (int.TryParse(input, out var choice) && Enum.IsDefined(typeof(GenerationMode), choice))
            {
                if (TryParseSelection(input, out var mode))
                {
                    return mode;
                }
            }

            System.Console.WriteLine(
                "Invalid selection. Please enter 0, 1, 2 or 3.");
        }
    }


    public static bool TryParseSelection(string? input, out GenerationMode mode)
    {
        mode = GenerationMode.Exit;

        if (!int.TryParse(input, out var choice))
        {
            return false;
        }

        if (!Enum.IsDefined(typeof(GenerationMode), choice))
        {
            return false;
        }

        mode = (GenerationMode)choice;
        return true;
    }

}
