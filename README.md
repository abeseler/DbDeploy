# DbDeploy

This is a simple database migration tool that can be used to manage database schema changes.
It currently supports PostgreSQL, MSSQL and SQLite.

## Design & Philosophy

DbDeploy grew out of experience with Flyway and Liquibase, keeping the parts that worked and dropping the parts that added friction. A few principles shape it:

- **The tool is not opinionated about structure — but it was designed for one file per object.** DbDeploy just applies the files your starting file points to, in order. You can use it the Flyway way: one `Migrations` folder full of versioned files whose names guarantee ordering. But it was built to also support a different model that I prefer — point it at your existing *object folders* (Tables, Views, Stored Procedures) and let those files *be* the migrations.

- **Your object folders can *be* your migrations.** A common pattern is to keep a folder of object definitions *and* a separate folder of migration/rollback scripts that duplicate those changes — two representations of the same change kept in sync by hand. DbDeploy makes the second folder optional: point it at your object folders and the files in them are the migrations. One source of truth.

- **One file per object, accumulating its history.** Because a file can hold multiple migration blocks, an object's whole change history can live in one place. This makes the model a hybrid:
  - *Tables* are typically **delta-based** — the file accumulates blocks (`create`, then `addColumn`, then `addColumn`), so the file is the object's history, not a snapshot of its current shape.
  - *Views, procedures and functions* are typically **state-based** — a single block with `runOnChange: true` that is re-applied whenever its SQL changes.

- **DbDeploy does not parse or validate your SQL.** It only parses the two things it must: the JSON migration header and the statement separators. Everything else is handed to the database, which is the authority on whether the SQL is valid. The tool's job is to *try to apply* your SQL in a known order and record what succeeded — not to understand it.

- **Ordering is by convention, not by parsing.** Because the tool doesn't understand your SQL, it can't infer dependencies. Execution order comes from the starting file's include order and alphabetical order within a folder. In practice this means either naming files to guarantee order (the single-folder approach) or grouping by dependency (for example, a separate folder for foreign keys applied after all tables exist, or a `Views2` folder for views that reference other views).

## Deployment Semantics

Understanding a few core behaviors will help you use DbDeploy safely:

- **Roll-forward only.** DbDeploy has no concept of "down" or rollback migrations. Recovery from a bad migration is always done by writing a new migration that corrects the problem. Design your changes accordingly.
- **Each migration is its own unit of work.** Migrations are applied one at a time, each in its own transaction (unless `runInTransaction` is `false`). There is no single transaction spanning the whole deployment. If migration 5 of 10 fails, migrations 1–4 remain applied and the process exits with a non-zero code. Re-running after fixing the problem will resume from the first unapplied migration.
- **Write idempotent, defensive SQL.** Because there is no rollback and each migration commits independently, migrations should be written so a partially-completed deployment can be safely re-run — e.g. `CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, guarded inserts, etc.
- **Migrations are identified by `fileName [title]` and a hash of their SQL.** Once a migration has been applied, editing its SQL changes the hash and causes the deployment to fail validation (unless `runOnChange` or `runAlways` is set). This is intentional — it prevents silently altering history. Note the hash is sensitive to the SQL text, so even reformatting/whitespace-only edits to an already-applied migration will trip this check.

### Deployment Lock

DbDeploy holds a **session-scoped lock** for the duration of a deployment so that only one runs at a time. The lock is tied to the database connection, so it is **released automatically if the process dies** (crash, OOM, cancelled pipeline job) — no manual cleanup is required. Each provider uses its native mechanism:

- **PostgreSQL**: a session-level advisory lock (`pg_advisory_lock`), scoped to the target database.
- **MSSQL**: a session-scoped application lock (`sp_getapplock`).
- **SQLite**: an exclusive lock on the database file.

The `__migration_lock` table is still written as an audit trail (and to allocate a `deployment_id`), but it no longer controls mutual exclusion, so an abandoned `finished_on IS NULL` row from a killed run is harmless.

The lock is acquired only for the database phase of a run — migration files are parsed beforehand, so a large migration set does not hold the lock while parsing.

`--maxLockWait` bounds how long DbDeploy waits for a contended lock. For PostgreSQL and MSSQL this is honored precisely. For SQLite it is best-effort: because the SQLite lock is a file lock that also blocks table setup, a competing deployment waits for the other to finish rather than timing out exactly.

### Dry Run

The `dryrun` command reports the same pending/applied/synced/filtered counts as `status`, but also writes the SQL that *would* be applied to a plan file (`--outputFile`, default `dbdeploy-plan.sql`). It does not acquire the deployment lock and applies nothing.

This is useful as a review/approval gate in a pipeline: generate the plan, publish it as an artifact for a human to review, then run `update` once approved.

The plan reflects the exact execution order and applies the same context filtering as a real deployment, so it only contains the migrations that would actually run. Each migration is annotated with its identity and its `runInTransaction`, `timeout` and `onError` settings. Only pending migrations are included; migrations that would be marked as applied without executing (see `sync`) are counted but not written.

## Configuration

The configuration can be done via command line arguments. The following arguments are available:

- `--command`: The command to execute. Possible values are `update`, `status`, `sync` and `dryrun`.
- `--startingFile`: The starting file. This is a json file that contains the files to include.
- `--maxLockWait`: The maximum time to wait for the lock in seconds. Default is 120 seconds.
- `--contexts`: The contexts to use. Multiple contexts can be separated by a comma.
- `--provider`: The provider to use. Possible values are `postgres`, `mssql` and `sqlite`.
- `--connectionString`: The connection string to use.
- `--connectionAttempts`: The number of initial connection attempts. Default is 10.
- `--connectionRetryDelay`: The delay between connection attempts in seconds. Default is 5 seconds.
- `--outputFile`: The file the `dryrun` command writes the migration plan to. Default is `dbdeploy-plan.sql`.
- `--logLevel`: The log level to use. Possible values are `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`. Default is `Information`.

The root directory is `/Migrations`. This is the parent directory of the starting file and all the files that are included.

### Running the Container

The container is available on [Docker Hub](https://hub.docker.com/r/abeseler/dbdeploy).

You can use the command line arguments above or the following environment variables for configuration:

- `Deploy__Command`: The command to execute. Possible values are `update`, `status`, `sync` and `dryrun`.
- `Deploy__StartingFile`: The starting file. This is a json file that contains the files to include.
- `Deploy__LockWaitMaxSeconds`: The maximum time to wait for the lock in seconds. Default is 120 seconds.
- `Deploy__Contexts`: The contexts to use. Multiple contexts can be separated by a comma.
- `Deploy__DatabaseProvider`: The provider to use. Possible values are `postgres`, `mssql` and `sqlite`.
- `Deploy__ConnectionString`: The connection string to use.
- `Deploy__ConnectionAttempts`: The number of initial connection attempts. Default is 10.
- `Deploy__ConnectionRetryDelaySeconds`: The delay between connection attempts in seconds. Default is 5 seconds.
- `Deploy__OutputFile`: The file the `dryrun` command writes the migration plan to. Default is `dbdeploy-plan.sql`.
- `Serilog__MinimumLevel__Default`: The log level to use. Possible values are `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`. Default is `Information`.

To mount your migrations, you can mount a volume to `/app/Migrations`.

## Starting File

The starting file is a json file that contains an array of includes. The following is an example of a starting file:
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

Migrations are executed in the order they are included in the starting file. If a directory is included, the files are executed in alphabetical order.

DbDeploy is not opinionated about how you organize your migrations. However, generally I prefer to have 1 folder per type of object (Tables, Views, Stored Procedures, etc.) and then 1 file per object. This makes it easier to manage and track changes. Then just include your folders by dependency order in the starting file (for example, Views require Tables to exist, so apply Table migrations before Views).

## Migrations

Migrations are just SQL files. They can be named anything you want. The only requirement is that they are in the `/Migrations` directory. The files can contain 1 or more migrations and each migration can contain 1 or more statements. The statements are separated by a line that starts with `--NewStatement`.

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
    created_on_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
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

- `title`: *REQUIRED* The title of the migration. This can be any string you want but must be unique within the migration file.
- `dependsOn`: An array of migration files this migration must run after. See [Dependencies](#dependencies) below.
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

Again, DbDeploy is not opinionated about how you organize your migrations. However, because of the way files can contain multiple migrations, having 1 file per object means you get all the history of that object in 1 place.

## Dependencies

By default, migrations run in the order they are included (starting file order, then alphabetical within a directory). For most cases, grouping folders by dependency in the starting file is enough (for example, apply `Tables` before `Views`).

When folder ordering is not enough — or when you want a migration's ordering requirement to be explicit and reorder-safe — a migration can declare `dependsOn`:

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

Behavior and rationale:

- **References are by file, not by title.** A file is the natural, filesystem-unique key, and you never have to edit the file you depend on. Depending on a file means "after *all* in-context migrations in that file."
- **Ordering stays convention-first.** `dependsOn` only adds constraints on top of the existing include order; a stable topological sort keeps everything else where it was. Declaring nothing behaves exactly as before.
- **Paths are normalized for you.** Separators (`\` or `/`) and a leading slash don't matter, and matching is case-insensitive — so you don't have to mirror the tool's internal path format.
- **Validation is fail-fast, before any SQL runs.** The deployment stops with a clear error if a reference is invalid (see edge cases below) or forms a dependency cycle (the cycle path is printed).

Because DbDeploy does not parse your SQL, it cannot infer dependencies — `dependsOn` is pure ordering metadata that you opt into where it matters.

### Dependency edge cases

DbDeploy does not track deleted migrations, and `dependsOn` is designed to stay consistent with that:

- **The referenced file exists → it is ordered before the dependent.** Removing a file that nothing references simply changes the order of what remains; because already-applied migrations are skipped regardless of position, this is safe and produces no error.
- **The referenced file is missing but was already applied → the reference is treated as satisfied.** You can delete an old migration file even if something still declares `dependsOn` on it; since it already ran, the ordering constraint is already met and no error is raised.
- **The referenced file is missing and was never applied → hard error.** This is almost always a typo or a genuinely missing dependency, so the deployment stops rather than silently running in the wrong order.
- **The reference matches more than one file (case-insensitively) → hard error.** Ambiguous references are rejected.
- **The referenced migrations are all excluded by the active context → hard error.** Depending on something that will not run in the current context is treated as a misconfiguration.
- **The dependencies form a cycle → hard error**, with the cycle path printed so you can see which declarations to break.

The `dryrun` command writes the fully resolved order to its plan file, and appends an informational footer noting any migration that now applies in a different relative order than it did in the target database (based on the recorded apply sequence). That surfaces a folder/file reordering that would be fine on the current database but could fail on a fresh one — a hint to declare a `dependsOn` if the order is actually required.
