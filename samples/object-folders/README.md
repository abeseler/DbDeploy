# Object-folder starter

Copy this directory to `Migrations` (or point `--migrations` at it). Rename folders if you want — Ratchet only applies what `ratchet.json` lists, in that order.

I use **one folder per object type** and **one file per object**. Tables accumulate `once` blocks (`create`, then `addColumn`). Indexes live in the table (or view) file, not their own folder. Views, functions, and procedures are usually a single `onChange` block. Seed upserts that must win over in-database edits are `always`.

`PreDeploy` and `PostDeploy` are for one-off scripts that are not an object definition: a backfill, a data fix, something that should run before any objects change or after they all exist. Same `/* Migration */` headers as everything else; usually `run: once`.

Folders are listed in dependency order. They are not walked recursively, so `Tables` then `ForeignKeys` is intentional. Insert a new stage by adding a line to the include array. Do not add `dependsOn` unless that order is not enough — a view that must follow one block in a long table file, for example.

| Folder | Typical contents |
|---|---|
| `PreDeploy` | one-off scripts that must run before object changes |
| `Extensions` | `CREATE EXTENSION` |
| `Schemas` | `CREATE SCHEMA` |
| `Types` | enums, domains, composites |
| `Sequences` | `CREATE SEQUENCE` (skip if you only use identity columns) |
| `Tables` | `CREATE TABLE`, later `ALTER`s, and indexes for that table |
| `ForeignKeys` | `ALTER TABLE … ADD CONSTRAINT` FKs |
| `Functions` | `CREATE OR REPLACE FUNCTION` |
| `Views` | `CREATE OR REPLACE VIEW` (indexes on the view go in this file) |
| `Procedures` | `CREATE OR REPLACE PROCEDURE` |
| `Triggers` | `CREATE TRIGGER` |
| `Grants` | `GRANT` / `REVOKE` |
| `PostDeploy` | one-off scripts that must run after objects exist |
| `Seed` | lookup upserts; only when `--contexts seed` |

Empty folders are fine. The stubs under `Types`, `Sequences`, `Tables`, `Views`, and `Seed` are Postgres so you can see headers; delete them and put your objects in.

A Flyway-style dump in one folder is also fine. This tree is a convention, not a requirement.
