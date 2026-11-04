# @ogz/crm-entities

TypeScript definitions for Dataverse tables, generated from Dataverse metadata.

- `src/generated/` — **generated only. Never edit by hand.** Written by `SupportTicket.EntityGenerator`.
- `src/index.ts` — handwritten public entry point; only re-exports from `generated`.

Planned content per table (e.g. `ogz_support_ticket`, `ogz_ticket_category`): logical name,
entity set name, attribute logical names, attribute types, Choice (option set) values and other
relevant metadata.

Build: `npm run build` (emits ESM + `.d.ts` to `dist/`). Consumed by `@ogz/crm-client`
through the npm workspace, where it is bundled into each Web Resource.
