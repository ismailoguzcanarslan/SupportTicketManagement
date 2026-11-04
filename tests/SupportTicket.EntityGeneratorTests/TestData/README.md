# Generator test data

Offline fixtures for testing the generator. Tests never connect to Dataverse.

| Folder | Content |
| --- | --- |
| `Metadata/` | Table metadata snapshots used as generator input, e.g. `ogz_support_ticket.json`. Capture each one once from a dev environment and trim it to what the tests need. |
| `ExpectedOutput/TypeScript/` | Golden files: the exact TypeScript the generator must produce for the metadata above. |
| `ExpectedOutput/CSharp/` | Golden files for any C# the generator itself writes. C# produced by `pac modelbuilder` is Microsoft's output and is not snapshot-tested. |

Everything here is copied to the test output folder. Read it through `Infrastructure/TestData`.

Metadata fixtures must not contain secrets, environment URLs or real personal data.
