# SupportTicket.EntityGenerator

Console tool that will generate entity definitions from Dataverse metadata. **Not implemented yet.**

## Planned pipeline

1. **Connect** to Dataverse (`Configuration/`, `Dataverse/`). The environment URL and credentials
   come from `dotnet user-secrets` or environment variables — never from a committed file.
   `generator.settings.json` holds only non-secret settings (tables, output paths).
2. **Retrieve metadata** for the tables listed in `generator.settings.json` (`Dataverse/`).
3. **Generate TypeScript** (`Generation/TypeScript/`) into
   `src/frontend/crm-entities/src/generated` — custom, because no Microsoft tool produces the
   shape we want for Web Resources.
4. **Generate C#** (`Generation/CSharp/`) into `src/dataverse/SupportTicket.Entities/GeneratedCode`
   by invoking Microsoft's supported **`pac modelbuilder build`** with an entity filter, rather
   than reimplementing early-bound generation.

Run (today it only prints a message):

```bash
dotnet run --project src/tools/SupportTicket.EntityGenerator
```
