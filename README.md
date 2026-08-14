# Ratchet

This is a simple database migration tool that can be used to manage database schema changes.
It currently supports PostgreSQL, MSSQL and SQLite.

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

See [Configuration](#configuration) for all options.

## Design & Philosophy

Ratchet grew out of experience with Flyway and Liquibase, keeping the parts that worked and dropping the parts that added friction. A few principles shape it:

- **The tool is not opinionated about structure — but it was designed for one file per object.** Ratchet just applies the files your starting file points to, in order. You can use it the Flyway way: one `Migrations` folder full of versioned files whose names guarantee ordering. But it was built to also support a different model that I prefer — point it at your existing *object folders* (Tables, Views, Stored Procedures) and let those files *be* the migrations.

- **Your object folders can *be* your migrations.** A common pattern is to keep a folder of object definitions *and* a separate folder of migration/rollback scripts that duplicate those changes — two representations of the same change kept in sync by hand. Ratchet makes the second folder optional: point it at your object folders and the files in them are the migrations. One source of truth.

- **One file per object, accumulating its history.** Because a file can hold multiple migration blocks, an object's whole change history can live in one place. This makes the model a hybrid:
  - *Tables* are typically **delta-based** — the file accumulates blocks (`create`, then `addColumn`, then `addColumn`), so the file is the object's history, not a snapshot of its current shape.
  - *Views, procedures and functions* are typically **state-based** — a single block with `runOnChange: true` that is re-applied whenever its SQL changes.

- **Ratchet does not parse or validate your SQL.** It only parses the two things it must: the JSON migration header and the statement separators. Everything else is handed to the database, which is the authority on whether the SQL is valid. The tool's job is to *try to apply* your SQL in a known order and record what succeeded — not to understand it.

- **Roll-forward only — on purpose.** Rollback scripts are often sold as a safety net, but that safety is partly an illusion. A rollback restores *structure*, not *data*: drop a column and the "undo" can add the column back, but the values are gone. Rollback SQL can also fail exactly like forward SQL — a bug, a lock, a timeout — and, because Ratchet doesn't parse SQL, a "down" script is no more verifiable than any other migration. An automated rollback would therefore imply a guarantee that doesn't actually exist. When a deployment goes sideways, the right response is human judgment, not a canned reverse script: sometimes it's a syntax error you fix in the migration and re-apply; sometimes an expert has to look at the failure and decide — hand-edit the database into a good state, or write new migrations that correct forward. There is no single guaranteed answer, so Ratchet doesn't pretend to offer one.

- **Ordering is by convention first, with explicit overrides.** Because the tool doesn't understand your SQL, it can't *infer* dependencies. The default order comes from the starting file's include order and alphabetical order within a folder — so you group by dependency (for example, a separate folder for foreign keys applied after all tables exist, or a `Views2` folder for views that reference other views). When convention isn't enough, a migration can declare an explicit `dependsOn` (see [Dependencies](#dependencies)). That stays true to the no-parse philosophy: `dependsOn` is ordering *metadata* you declare, not something inferred from the SQL.

## Commands

Ratchet runs a single command per invocation, selected with `--command` (or `Deploy__Command`). `--help` / `-h` prints usage and exits without connecting to the database.

- **`update`** — Apply all pending migrations in resolved order. This is the normal deployment command. It runs SQL; it does not write history for migrations it did not apply. The summary reports how many were applied, skipped (`onError: Skip`), marked (`onError: Mark`), and filtered out. Checksum drift fails the run — see `repair`.
- **`status`** — Report pending apply, pending baseline, needs repair, already applied, and filtered-out counts. Read-only; applies nothing and takes no lock. Checksum mismatches are logged as "needs repair" (they fail `update`).
- **`baseline`** — Record migrations that have **no history row** (or a leftover null hash) as already applied, **without running SQL**. Use this when adopting Ratchet on a database whose schema already exists. It will not overwrite an existing hash — that is `repair`.
- **`repair`** — Update the stored hash of migrations that **already have a history row** whose SQL no longer matches. Use this after you have fixed the database by hand and accept the current files as the new truth. It does not insert missing rows — that is `baseline`. It does not run SQL.
- **`dryrun`** — Like `status`, but also writes the exact SQL that `update` would run to a plan file for review. Applies nothing and takes no lock. See [Dry Run](#dry-run).

## Deployment Semantics

Understanding a few core behaviors will help you use Ratchet safely:

- **Roll-forward only.** Ratchet has no concept of a "down" or rollback migration. Recovery from a bad change is a human decision — fix the migration and re-apply, correct forward with a new migration, or have someone resolve the database state directly — rather than an automated reverse script. See [Design & Philosophy](#design--philosophy) for the reasoning.
- **Each migration is its own unit of work.** Migrations are applied one at a time, each in its own transaction (unless `runInTransaction` is `false`). There is no single transaction spanning the whole deployment. If migration 5 of 10 fails, migrations 1–4 remain applied and the process exits with a non-zero code. Re-running after fixing the problem will resume from the first unapplied migration. A failure to connect to the database after the configured retries also exits non-zero.
- **`onError: Skip` is not a success.** A skipped migration is logged, is **not** recorded as applied, does **not** consume an apply sequence number, and will be retried on the next run. `onError: Mark` records the migration as applied (and sequences it) so it will not be retried. The `update` summary counts Applied, Skipped, and Marked separately.
- **Idempotency matters only for migrations that can re-run mid-change.** An already-applied migration is never re-run, so defensive guards like `CREATE TABLE IF NOT EXISTS` are unnecessary for the common case — that `CREATE TABLE` migration runs exactly once. A migration in a transaction (the default) that fails simply rolls back and re-runs cleanly next time. Guards matter in two situations: (1) a migration with `runInTransaction: false` that fails partway, since its earlier statements have already committed and the whole migration re-runs on the next attempt; and (2) `runOnChange`/`runAlways` migrations, which re-execute by design. Write those so re-running is safe.
- **Migrations are identified by `fileName [title]` and a hash of their SQL.** Once a migration has been applied, editing its SQL changes the hash and causes `update` to fail validation (unless `runOnChange` or `runAlways` is set). This is intentional — it prevents silently altering history. Note the hash is sensitive to the SQL text, so even reformatting/whitespace-only edits to an already-applied migration will trip this check. If the database already matches the edited file, run `repair` to accept the new hash.
- **Writing history without SQL is never implicit.** `update` only records migrations it actually applied (or `onError: Mark`). Stamping existing schema is `baseline`. Accepting a new hash for something already recorded is `repair`.
- **Apply order is recorded globally.** Each first-time apply (or `onError: Mark`) gets the next `executed_sequence` across all deployments — not a counter that restarts at 1 every run. Re-applying a `runOnChange` / `runAlways` migration keeps its original sequence so dry-run reorder detection still reflects first-apply order.

### Deployment Lock

Ratchet holds a **session-scoped lock** for the duration of a deployment so that only one runs at a time. The lock is tied to the database connection, so it is **released automatically if the process dies** (crash, OOM, cancelled pipeline job) — no manual cleanup is required. Each provider uses its native mechanism:

- **PostgreSQL**: a session-level advisory lock (`pg_advisory_lock`), scoped to the target database.
- **MSSQL**: a session-scoped application lock (`sp_getapplock`).
- **SQLite**: an exclusive lock on the database file.

The `__migration_lock` table is still written as an audit trail (and to allocate a `deployment_id`), but it no longer controls mutual exclusion, so an abandoned `finished_on IS NULL` row from a killed run is harmless.

The lock is acquired only for the database phase of a run — migration files are parsed beforehand, so a large migration set does not hold the lock while parsing.

`--maxLockWait` bounds how long Ratchet waits for a contended lock. For PostgreSQL and MSSQL this is honored precisely. For SQLite it is best-effort: because the SQLite lock is a file lock that also blocks table setup, a competing deployment waits for the other to finish rather than timing out exactly.

### Dry Run

The `dryrun` command reports the same pending apply / baseline / repair / filtered counts as `status`, but also writes the SQL that *would* be applied to a plan file (`--outputFile`, default `ratchet-plan.sql`). It does not acquire the deployment lock and applies nothing.

This is useful as a review/approval gate in a pipeline: generate the plan, publish it as an artifact for a human to review, then run `update` once approved.

The plan reflects the exact execution order and applies the same context filtering as a real deployment, so it only contains the migrations that `update` would actually run. Each migration is annotated with its identity and its `runInTransaction`, `timeout` and `onError` settings. Pending baseline and needs-repair counts are reported but those migrations are not written to the plan file.

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

The configuration can be done via command line arguments. The following arguments are available:

- `--command`: The command to execute. Possible values are `update`, `status`, `baseline`, `repair` and `dryrun`.
- `--migrations`: Directory containing the starting file and SQL. Relative paths are resolved from the process working directory. Default is `Migrations`.
- `--startingFile`: The starting file. This is a json file that contains the files to include, or a single `.sql` file. Default is `ratchet.json` in the working directory.
- `--help`, `-h`: Print usage and exit.
- `--maxLockWait`: The maximum time to wait for the lock in seconds. Default is 120 seconds.
- `--contexts`: The contexts to use. Multiple contexts can be separated by a comma.
- `--provider`: The provider to use. Possible values are `postgres`, `mssql` and `sqlite`.
- `--connectionString`: The connection string to use.
- `--connectionAttempts`: The number of initial connection attempts. Default is 10.
- `--connectionRetryDelay`: The delay between connection attempts in seconds. Default is 5 seconds.
- `--outputFile`: The file the `dryrun` command writes the migration plan to. Default is `ratchet-plan.sql`.
- `--logLevel`: The log level to use. Possible values are `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`. Default is `Information`.

The default working directory is `Migrations` (in the container: `/app/Migrations`). This is the parent directory of the starting file and all the files that are included. Override it with `--migrations` or `Deploy__WorkingDirectory`.

### Running the Container

The container is available on [Docker Hub](https://hub.docker.com/r/abeseler/ratchet).

You can use the command line arguments above or the following environment variables for configuration:

- `Deploy__Command`: The command to execute. Possible values are `update`, `status`, `baseline`, `repair` and `dryrun`.
- `Deploy__WorkingDirectory`: Directory containing the starting file and SQL. Default is `Migrations`.
- `Deploy__StartingFile`: The starting file. This is a json file that contains the files to include, or a single `.sql` file. Default is `ratchet.json`.
- `Deploy__LockWaitMaxSeconds`: The maximum time to wait for the lock in seconds. Default is 120 seconds.
- `Deploy__Contexts`: The contexts to use. Multiple contexts can be separated by a comma.
- `Deploy__DatabaseProvider`: The provider to use. Possible values are `postgres`, `mssql` and `sqlite`.
- `Deploy__ConnectionString`: The connection string to use.
- `Deploy__ConnectionAttempts`: The number of initial connection attempts. Default is 10.
- `Deploy__ConnectionRetryDelaySeconds`: The delay between connection attempts in seconds. Default is 5 seconds.
- `Deploy__OutputFile`: The file the `dryrun` command writes the migration plan to. Default is `ratchet-plan.sql`.
- `Serilog__MinimumLevel__Default`: The log level to use. Possible values are `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`. Default is `Information`.

To mount your migrations, you can mount a volume to `/app/Migrations`.

## Starting File

If `--startingFile` / `Deploy__StartingFile` is omitted, Ratchet looks for **`ratchet.json`** in the working directory (`Migrations` by default; `/app/Migrations` in the container).

The starting file is a json file that contains an array of includes. Files and directories in `include` are classified by what exists on disk — a folder named `v2.0` is a directory, not a file. The following is an example of a starting file:
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

The following properties are available:

- `include`: The files or directories to include.
- `contextFilter`: The required contexts for the include. If one of the contexts is not provided, the migration(s) of the include will be skipped.
- `contextRequired`: If a context is required. Default is `false`.
- `errorIfMissingOrEmpty`: If an error should be thrown if the included file or directory is missing or empty. Default is `true`.

Migrations are executed in the order they are included in the starting file. If a directory is included, the files in that directory are executed in alphabetical order (the directory itself is not walked recursively).

As described in [Design & Philosophy](#design--philosophy), the layout I recommend is one folder per object type (Tables, Views, Stored Procedures, etc.) and one file per object, with folders included in dependency order (for example, apply Tables before Views).

## Migrations

Migrations are just SQL files. They can be named anything you want. They must live under the working directory (`Migrations` by default). The files can contain 1 or more migrations and each migration can contain 1 or more statements. The statements are separated by a line that starts with `--NewStatement`.

A migration is a block of SQL preceded by a multi-line comment that contains the migration properties.
The comment must start with `/* Migration` and end with `*/`. The properties are in JSON format and must be valid JSON.

The following is an example of a migration file:
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

The following properties are available:

- `title`: *REQUIRED* The title of the migration. This can be any string you want but must be unique within the migration file (case-insensitive, so `create` and `CREATE` collide).
- `dependsOn`: An array of migrations this one must run after. Each entry is a file path (after every block in that file) or `file#title` (after that one block). See [Dependencies](#dependencies) below.
- `runAlways`: If the migration should be run every time. Default is `false`.
- `runOnChange`: If the migration should be run when the migration changes. Default is `false`.
- `runInTransaction`: If the migration should be run in a transaction. Default is `true`.
- `contextFilter`: The required contexts for the migration. If one of the contexts is not provided, the migration will be skipped.
- `contextRequired`: If a context is required. Default is `false`.
- `timeout`: The timeout in seconds for the migration. Default is `30`.
- `onError`: How to handle a failure of this migration. Default is `Fail`.
  - `Fail`: Stop the deployment and exit with a non-zero code.
  - `Skip`: Log the error, do **not** record the migration as applied, and continue to the next migration. It will be retried on the next run.
  - `Mark`: Log the error but record the migration as **successfully applied** so it will not be retried. ⚠️ Use with caution — this hides a real failure and can lead to schema drift.

The `runAlways` property is useful for migrations that need to be run every time the database is updated. For example, if you need to update a lookup table with new values, you would set `runAlways` to `true`.
The `runOnChange` property is useful for migrations that need to be run when the migration changes. For example, if you need to update a view or stored procedure, you would set `runOnChange` to `true`.

Again, because a file can contain multiple migrations, keeping one file per object means the full change history of that object lives in one place.

## Dependencies

By default, migrations run in the order they are included (starting file order, then alphabetical within a directory). For most cases, grouping folders by dependency in the starting file is enough (for example, apply `Tables` before `Views`).

When folder ordering is not enough — or when you want a migration's ordering requirement to be explicit and reorder-safe — a migration can declare `dependsOn`.

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

Behavior and rationale:

- **File references are the default.** Depending on a file means "after *all* in-context migrations in that file." You never have to edit the file you depend on.
- **`file#title` is the escape hatch.** It orders after that one block only. Title matching is case-insensitive. The file part is still required — a bare `#title` is rejected.
- **Ordering stays convention-first.** `dependsOn` only adds constraints on top of the existing include order; a stable topological sort keeps everything else where it was. Declaring nothing behaves exactly as before.
- **Paths are normalized for you.** Separators (`\` or `/`) and a leading slash don't matter, and matching is case-insensitive — so you don't have to mirror the tool's internal path format.
- **Validation is fail-fast, before any SQL runs.** The deployment stops with a clear error if a reference is invalid (see edge cases below) or forms a dependency cycle (the cycle path is printed).

Because Ratchet does not parse your SQL, it cannot infer dependencies — `dependsOn` is pure ordering metadata that you opt into where it matters.

### Dependency edge cases

Ratchet does not track deleted migrations, and `dependsOn` is designed to stay consistent with that:

- **The referenced file or block exists → it is ordered before the dependent.** Removing a file that nothing references simply changes the order of what remains; because already-applied migrations are skipped regardless of position, this is safe and produces no error.
- **A file reference is missing but that file was already applied → the reference is treated as satisfied.** You can delete an old migration file even if something still declares `dependsOn` on it; since it already ran, the ordering constraint is already met and no error is raised. Any applied block from that file counts.
- **A `file#title` reference is missing but that specific block was already applied → satisfied.** A different title from the same file being applied is **not** enough — title references are exact (case-insensitive).
- **The referenced file or block is missing and was never applied → hard error.** This is almost always a typo or a genuinely missing dependency, so the deployment stops rather than silently running in the wrong order. An empty `#` fragment (`orders.sql#`) is also a hard error.
- **The reference matches more than one file, or more than one title in that file (case-insensitively) → hard error.** Ambiguous references are rejected.
- **The referenced migrations are all excluded by the active context → hard error.** Depending on something that will not run in the current context is treated as a misconfiguration.
- **The dependencies form a cycle → hard error**, with the cycle path printed so you can see which declarations to break.

The `dryrun` command writes the fully resolved order to its plan file, and appends an informational footer noting any migration that now applies in a different relative order than it did in the target database (based on the recorded apply sequence). That surfaces a folder/file reordering that would be fine on the current database but could fail on a fresh one — a hint to declare a `dependsOn` if the order is actually required.

## Known Limitations

These are deliberate tradeoffs, called out here so they are not surprises:

- **No rollback.** Ratchet is roll-forward only — recovery is a deliberate human decision rather than an automated reverse script. See [Design & Philosophy](#design--philosophy) and [Deployment Semantics](#deployment-semantics).
- **Atomicity is per migration, not per deployment.** A failure part-way through leaves earlier migrations applied; re-running resumes from the first unapplied migration rather than restarting the whole deployment. See [Deployment Semantics](#deployment-semantics).
- **Statement splitting is textual.** Statements are separated by lines beginning with `--NewStatement`. Because Ratchet does not parse SQL, a line that begins with that token inside a string literal or comment would split incorrectly. Keep the separator on its own dedicated line.
- **Hashing is for change detection, not security.** Applied migrations are fingerprinted with MD5 to detect edits after they were applied; it is not a cryptographic guarantee.
- **Three databases.** Only PostgreSQL, MSSQL and SQLite are supported.
