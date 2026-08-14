# Ratchet

Ratchet applies SQL migrations in a known order and records what succeeded. It does not parse your SQL, and it does not roll back. It supports PostgreSQL, SQL Server, and SQLite.

## Quick Start

Ratchet runs as a one-shot container: point it at a directory of SQL migrations, give it a connection string, and run a command. A typical CI/CD step pulls the image, mounts your migrations to `/app/Migrations`, and runs `update`:

```bash
docker run --rm \
  -v ./migrations:/app/Migrations \
  -e Deploy__Command=update \
  -e Deploy__DatabaseProvider=postgres \
  -e Deploy__ConnectionString="Host=db;Database=app;Username=postgres;Password=..." \
  abeseler/ratchet
```

Put a `ratchet.json` starting file in that mounted directory (see [Starting File](#starting-file)). Override the name with `--startingFile` / `Deploy__StartingFile` if you need to.

Locally, the defaults are a `Migrations` working directory and `ratchet.json` inside it. See [Configuration](#configuration) for all options.

## Design & Philosophy

Ratchet grew out of experience with Flyway and Liquibase, keeping the parts that worked and dropping the parts that added friction. A few principles shape it:

- **It applies whatever the starting file lists. It was designed for one file per object.** You can use it the Flyway way: one `Migrations` folder full of versioned files whose names guarantee ordering. I prefer a different model — point it at your existing *object folders* (Tables, Views, Stored Procedures) and let those files *be* the migrations.

- **Your object folders can *be* your migrations.** A common pattern is to keep a folder of object definitions *and* a separate folder of migration/rollback scripts that duplicate those changes — two representations of the same change kept in sync by hand. Ratchet makes the second folder optional: point it at your object folders and the files in them are the migrations. One source of truth.

- **One file per object, accumulating its history.** Because a file can hold multiple migration blocks, an object's whole change history can live in one place. This makes the model a hybrid:
  - *Tables* are typically **delta-based** — the file accumulates blocks (`create`, then `addColumn`, then `addColumn`), so the file is the object's history, not a snapshot of its current shape.
  - *Views, procedures and functions* are typically **state-based** — a single block with `runOnChange: true` that is re-applied whenever its SQL changes.

- **Ratchet does not parse or check your SQL.** It only parses the two things it must: the JSON migration header and the statement separators. Everything else is handed to the database, which is the authority on whether the SQL is valid. The tool's job is to *try to apply* your SQL in a known order and record what succeeded — not to understand it.

- **Roll-forward only — on purpose.** Rollback scripts are often sold as a safety net, but that safety is partly an illusion. A rollback restores *structure*, not *data*: drop a column and the "undo" can add the column back, but the values are gone. Rollback SQL can also fail exactly like forward SQL — a bug, a lock, a timeout — and, because Ratchet doesn't parse SQL, a "down" script is no more verifiable than any other migration. An automated rollback would therefore imply a guarantee that doesn't actually exist. When a deployment goes sideways, the right response is human judgment, not a canned reverse script: sometimes it's a syntax error you fix in the migration and re-apply; sometimes an expert has to look at the failure and decide — hand-edit the database into a good state, or write new migrations that correct forward. There is no single guaranteed answer, so Ratchet doesn't pretend to offer one.

- **Ordering is by convention first, with explicit overrides.** Because the tool doesn't understand your SQL, it can't *infer* dependencies. The default order comes from the starting file's include order and alphabetical order within a folder — so you group by dependency (for example, a separate folder for foreign keys applied after all tables exist, or a `Views2` folder for views that reference other views). When convention isn't enough, a migration can declare an explicit `dependsOn` (see [Dependencies](#dependencies)). That stays true to the no-parse philosophy: `dependsOn` is ordering *metadata* you declare, not something inferred from the SQL.

## Commands

Ratchet runs a single command per invocation, selected with `--command` (or `Deploy__Command`). `--help` / `-h` prints usage and exits without connecting to the database.

- **`update`** — Apply pending migrations in resolved order. This is the normal deployment command. It runs SQL and records only what it applied (or `onError: Mark`). Checksum drift fails the run — see `repair`.
- **`status`** — Print pending apply, pending baseline, needs repair, already applied, and filtered-out counts. Read-only; no lock. Drift is a warning here and a failure on `update`.
- **`validate`** — Fail if the files cannot be parsed or ordered, or (when a database is configured) if any applied migration needs `repair`. Pending apply and pending baseline do not fail. See [Validate](#validate).
- **`dryrun`** — Like `status`, plus the SQL that `update` would run, written to a plan file. See [Dry Run](#dry-run).
- **`baseline`** — Record migrations that have no history row as already applied, without running SQL. For adopting Ratchet on an existing schema. Does not overwrite an existing hash — that is `repair`.
- **`repair`** — Update the stored hash of migrations that already have a history row whose SQL no longer matches. For after you fixed the database by hand. Does not insert missing rows — that is `baseline`.

## Deployment Semantics

- **Roll-forward only.** There is no down script. Recovery is a human decision — see [Design & Philosophy](#design--philosophy).
- **Each migration is its own unit of work.** Migrations are applied one at a time, each in its own transaction (unless `runInTransaction` is `false`). There is no single transaction spanning the whole deployment. If migration 5 of 10 fails, migrations 1–4 remain applied and the process exits with a non-zero code. Re-running after fixing the problem will resume from the first unapplied migration. A failure to connect to the database after the configured retries also exits non-zero.
- **`onError: Skip` is not a success.** A skipped migration is logged, is **not** recorded as applied, does **not** consume an apply sequence number, and will be retried on the next run. `onError: Mark` records the migration as applied (and sequences it) so it will not be retried. The `update` summary counts Applied, Skipped, and Marked separately.
- **Idempotency matters only for migrations that can re-run mid-change.** An already-applied migration is never re-run, so defensive guards like `CREATE TABLE IF NOT EXISTS` are unnecessary for the common case — that `CREATE TABLE` migration runs exactly once. A migration in a transaction (the default) that fails simply rolls back and re-runs cleanly next time. Guards matter in two situations: (1) a migration with `runInTransaction: false` that fails partway, since its earlier statements have already committed and the whole migration re-runs on the next attempt; and (2) `runOnChange`/`runAlways` migrations, which re-execute by design. Write those so re-running is safe.
- **Migrations are identified by `fileName [title]` and a hash of their SQL.** Once a migration has been applied, editing its SQL changes the hash and causes `update` to fail (unless `runOnChange` or `runAlways` is set). This is intentional — it prevents silently altering history. The hash is sensitive to the SQL text, so even reformatting an already-applied migration will trip this check. If the database already matches the edited file, run `repair` to accept the new hash.
- **Writing history without SQL is never implicit.** `update` only records migrations it actually applied (or `onError: Mark`). Stamping existing schema is `baseline`. Accepting a new hash for something already recorded is `repair`.
- **Apply order is recorded globally.** Each first-time apply (or `onError: Mark`) gets the next `executed_sequence` across all deployments — not a counter that restarts at 1 every run. Re-applying a `runOnChange` / `runAlways` migration keeps its original sequence so dry-run reorder detection still reflects first-apply order.

### Deployment Lock

`update`, `baseline`, and `repair` hold a **session-scoped lock** so only one of them runs at a time. The lock is tied to the database connection, so it is **released automatically if the process dies** (crash, OOM, cancelled pipeline job) — no manual cleanup. Each provider uses its native mechanism:

- **PostgreSQL**: a session-level advisory lock (`pg_try_advisory_lock`), scoped to the target database.
- **SQL Server**: a session-scoped application lock (`sp_getapplock`).
- **SQLite**: an exclusive lock on the database file.

The `__migration_lock` table is an audit trail and allocates a `deployment_id`. Mutual exclusion is the session lock, so an abandoned `finished_on IS NULL` row from a killed run is harmless.

Migration files are parsed **before** the lock is taken, so a large set does not block other work while Ratchet reads SQL. After the lock, history is loaded once and used to resolve `dependsOn` and to build the apply / baseline / repair plan.

`--maxLockWait` is how long to wait for a contended lock (default 120 seconds). PostgreSQL and SQL Server honor that bound. SQLite is best-effort: the file lock can also block table setup, so a competing run may wait for the other to finish rather than time out exactly.

### Dry Run

`dryrun` prints the same counts as `status` and writes the SQL that `update` would run to a plan file (`--outputFile`, default `ratchet-plan.sql`). It does not take the lock and applies nothing.

Use it as a review gate: generate the plan, publish it as an artifact, run `update` once someone has looked at it.

The plan is the same order and context filter as a real `update`. Each block is annotated with its identity and its `runInTransaction`, `timeout`, and `onError` settings. Pending baseline and needs-repair are counted but not written.

### Validate

`validate` is a PR gate, not a dress rehearsal for `update`. It fails the process if the starting file or SQL headers cannot be parsed, includes are missing, titles collide, or `dependsOn` is invalid or cyclic. Pending apply and pending baseline do **not** fail — new migrations are expected.

Without `--connectionString` / `--provider`, only files are checked and no database is opened. With a connection string, checksums are compared to that database's `__migration_history`, and drift fails the run (the same condition that fails `update`). You do not need `validate` in front of `update` in a deploy job; `update` already stops before applying if those problems exist.

### Baseline vs Repair

Both commands write `__migration_history` and run no SQL. They refuse to do each other's job.

**Adopt Ratchet on an existing database** — the schema is already there, history is empty (or some folders were never recorded):

1. `baseline` — insert history for in-context migrations that have no row (or a leftover null hash). `executed_sequence` stays null; Ratchet did not apply these.
2. `status` — pending apply should be 0 (unless you also have `runAlways` / `runOnChange` files).
3. Later, add a new migration block and `update` — only the new block runs.

`repair` on an empty journal is a no-op. If you skip `baseline`, the next `update` will try to run `CREATE TABLE` against objects that already exist.

**Unstick after a hand-fix** — a deploy failed, someone fixed the database (and maybe the file), and you want the journal to match:

1. `repair` — overwrite hashes of already-recorded migrations whose SQL changed. Existing `executed_sequence` is kept. Each id is logged with previous and current hash.
2. If a hotfix file was applied by hand and never recorded, `baseline` that file (it still has no row).
3. `update` — continues from the first unapplied migration.

`baseline` will not clear checksum drift. `update` still fails until you `repair`.

Both commands take the deployment lock, honor `--contexts`, and can be re-run safely (a second run is a no-op once history matches).

## Configuration

Flags and environment variables set the same options. Env vars use the `Deploy__` prefix (and `__` for nesting). The image is on [Docker Hub](https://hub.docker.com/r/abeseler/ratchet). Mount your SQL at `/app/Migrations`.

| Flag | Environment | Default |
|---|---|---|
| `--command` | `Deploy__Command` | (required) `update`, `status`, `validate`, `dryrun`, `baseline`, `repair` |
| `--migrations` | `Deploy__WorkingDirectory` | `Migrations` (in the container: `/app/Migrations`) |
| `--startingFile` | `Deploy__StartingFile` | `ratchet.json` in the working directory |
| `--provider` | `Deploy__DatabaseProvider` | `postgres`, `mssql`, or `sqlite` |
| `--connectionString` | `Deploy__ConnectionString` | (required except file-only `validate`) |
| `--contexts` | `Deploy__Contexts` | comma-separated; empty means no extra context |
| `--maxLockWait` | `Deploy__LockWaitMaxSeconds` | `120` |
| `--connectionAttempts` | `Deploy__ConnectionAttempts` | `10` |
| `--connectionRetryDelay` | `Deploy__ConnectionRetryDelaySeconds` | `5` |
| `--outputFile` | `Deploy__OutputFile` | `ratchet-plan.sql` |
| `--help`, `-h` | | print usage and exit |
| `--logLevel` | `Serilog__MinimumLevel__Default` | `Information` (`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`) |

Relative `--migrations` paths are resolved from the process working directory.

## Starting File

If you omit `--startingFile`, Ratchet looks for **`ratchet.json`** in the working directory.

The starting file is a JSON array of includes. Files and directories in `include` are classified by what exists on disk — a folder named `v2.0` is a directory, not a file.

```json
[
  {
    "include": [
      "ensure_exists.sql",
      "Tables",
      "Views",
      "PostDeployScripts"
    ],
    "contextFilter": [],
    "contextRequired": false,
    "errorIfMissingOrEmpty": true
  },
  {
    "include": [
      "SeedScripts"
    ],
    "contextFilter": ["seed"],
    "contextRequired": true,
    "errorIfMissingOrEmpty": false
  }
]
```

- `include`: Files or directories to include.
- `contextFilter`: Contexts that must be active for this include. If none of them are provided, the include is skipped.
- `contextRequired`: If `true`, the include is skipped when no contexts are passed at all. Default is `false`.
- `errorIfMissingOrEmpty`: If `true` (the default), a missing file or directory fails the run. An empty directory is not an error; an included SQL file with no migration blocks is.

Migrations run in include order. Files in a directory run in alphabetical order. Directories are not walked recursively.

The layout I recommend is one folder per object type (Tables, Views, Stored Procedures, …) and one file per object, with folders included in dependency order (Tables before Views).

## Migrations

Migrations are SQL files under the working directory (`Migrations` by default). A file can hold one or more migrations, and a migration can hold one or more statements. Statements are separated by a line that starts with `--NewStatement`.

A migration is a block of SQL preceded by a comment that starts with `/* Migration` and ends with `*/`. The body of that comment is JSON.

```sql
/* Migration
{
    "title": "widget:createTable"
}
*/
CREATE TABLE IF NOT EXISTS widget (
    widget_id INT GENERATED ALWAYS AS IDENTITY,
    description TEXT NOT NULL,
    created_on_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT pk_widget PRIMARY KEY (widget_id)
);

/* Migration
{
    "title": "widget.last_modified_on:addColumn"
}
*/
ALTER TABLE widget
ADD COLUMN last_modified_on_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc');
```

- `title`: **Required.** Any string, unique within the file (case-insensitive: `create` and `CREATE` collide).
- `dependsOn`: Migrations this one must run after. Each entry is a file path (after every block in that file) or `file#title` (after that one block). See [Dependencies](#dependencies).
- `runAlways`: Run every time. Default `false`. Useful for lookup-table upserts.
- `runOnChange`: Run when the SQL hash changes. Default `false`. Useful for views, procedures, and functions.
- `runInTransaction`: Wrap the migration in a transaction. Default `true`.
- `contextFilter`: Contexts that must be active or this migration is skipped.
- `contextRequired`: If `true`, skip when no contexts are passed. Default `false`.
- `timeout`: Statement timeout in seconds. Default `30`.
- `onError`: What to do if this migration fails. Default `Fail`.
  - `Fail`: Stop and exit non-zero.
  - `Skip`: Log the error, do **not** record the migration as applied, continue. It will be retried next run.
  - `Mark`: Log the error but record the migration as applied so it will not be retried. Use with caution — this hides a real failure and can lead to schema drift.

Because a file can contain multiple migrations, one file per object means that object's full change history lives in one place.

## Dependencies

By default, migrations run in include order, then alphabetically within a directory. For most cases, grouping folders by dependency in the starting file is enough (Tables before Views).

When that is not enough — or when you want the requirement explicit and reorder-safe — declare `dependsOn`.

**File is the default.** A path with no `#` means "after *all* in-context migrations in that file":

```sql
/* Migration
{
    "title": "fk_orders_customer",
    "dependsOn": ["Tables/orders.sql", "Tables/customers.sql"]
}
*/
ALTER TABLE orders
ADD CONSTRAINT fk_orders_customer FOREIGN KEY (customer_id) REFERENCES customers (customer_id);
```

**Pin a single block** with `file#title` when you only need one migration in that file (for example the `CREATE TABLE`, not later `ALTER`s):

```sql
/* Migration
{
    "title": "vw_orders:create",
    "runOnChange": true,
    "dependsOn": ["Tables/orders.sql#orders:createTable"]
}
*/
CREATE OR REPLACE VIEW vw_orders AS
SELECT id, amount FROM orders;
```

- **File references are the default.** Depending on a file means after every in-context block in that file. You never have to edit the file you depend on.
- **`file#title` is the escape hatch.** It orders after that one block only. Title matching is case-insensitive. The file part is required — a bare `#title` is rejected.
- **Ordering stays convention-first.** `dependsOn` only adds constraints on top of include order; a stable topological sort keeps everything else where it was. Declaring nothing behaves exactly as before.
- **Paths are normalized.** `\` or `/` and a leading slash do not matter. Matching is case-insensitive.
- **Invalid references fail before any SQL runs.** A bad ref or a cycle stops the deployment; the cycle path is printed.

Because Ratchet does not parse your SQL, it cannot infer dependencies — `dependsOn` is ordering metadata you opt into where it matters.

### Dependency edge cases

Ratchet does not track deleted migrations, and `dependsOn` is designed to stay consistent with that:

- **The referenced file or block exists → it is ordered before the dependent.** Removing a file that nothing references simply changes the order of what remains; because already-applied migrations are skipped regardless of position, this is safe and produces no error.
- **A file reference is missing but that file was already applied → the reference is treated as satisfied.** You can delete an old migration file even if something still declares `dependsOn` on it; since it already ran, the ordering constraint is already met. Any applied block from that file counts.
- **A `file#title` reference is missing but that specific block was already applied → satisfied.** A different title from the same file being applied is **not** enough — title references are exact (case-insensitive).
- **The referenced file or block is missing and was never applied → hard error.** Almost always a typo or a genuinely missing dependency. An empty `#` fragment (`orders.sql#`) is also a hard error.
- **The reference matches more than one file, or more than one title in that file (case-insensitively) → hard error.**
- **The referenced migrations are all excluded by the active context → hard error.** Depending on something that will not run in this context is a misconfiguration.
- **The dependencies form a cycle → hard error**, with the cycle path printed.

`dryrun` writes the fully resolved order to its plan file, and appends a footer if anything now applies in a different relative order than it did in the target database (from `executed_sequence`). That is harmless on this database but could fail on a fresh one — a hint to declare a `dependsOn` if the order is actually required.

## Known Limitations

Deliberate tradeoffs, so they are not surprises:

- **No rollback.** Recovery is a human decision. See [Design & Philosophy](#design--philosophy).
- **Atomicity is per migration, not per deployment.** A failure part-way through leaves earlier migrations applied; re-running resumes from the first unapplied one. See [Deployment Semantics](#deployment-semantics).
- **Statement splitting is textual.** Statements are separated by lines beginning with `--NewStatement`. A line that begins with that token inside a string literal or comment would split incorrectly. Keep the separator on its own dedicated line.
- **Hashing is for change detection, not security.** Applied migrations are fingerprinted with MD5; it is not a cryptographic guarantee.
- **Three databases.** PostgreSQL, SQL Server, and SQLite.
