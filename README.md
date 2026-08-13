# DbDeploy

This is a simple database migration tool that can be used to manage database schema changes.
It currently supports PostgreSQL, MSSQL and SQLite.

## Deployment Semantics

Understanding a few core behaviors will help you use DbDeploy safely:

- **Roll-forward only.** DbDeploy has no concept of "down" or rollback migrations. Recovery from a bad migration is always done by writing a new migration that corrects the problem. Design your changes accordingly.
- **Each migration is its own unit of work.** Migrations are applied one at a time, each in its own transaction (unless `runInTransaction` is `false`). There is no single transaction spanning the whole deployment. If migration 5 of 10 fails, migrations 1–4 remain applied and the process exits with a non-zero code. Re-running after fixing the problem will resume from the first unapplied migration.
- **Write idempotent, defensive SQL.** Because there is no rollback and each migration commits independently, migrations should be written so a partially-completed deployment can be safely re-run — e.g. `CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, guarded inserts, etc.
- **Migrations are identified by `fileName [title]` and a hash of their SQL.** Once a migration has been applied, editing its SQL changes the hash and causes the deployment to fail validation (unless `runOnChange` or `runAlways` is set). This is intentional — it prevents silently altering history. Note the hash is sensitive to the SQL text, so even reformatting/whitespace-only edits to an already-applied migration will trip this check.

### Deployment Lock

DbDeploy acquires a row-based lock (in the `__migration_lock` table) so that only one deployment runs at a time. The lock is released automatically when the run finishes.

**If a deployment is force-killed** (e.g. an OOM or a cancelled pipeline job), the lock row may be left open (`finished_on IS NULL`), which will block subsequent deployments until it is cleared manually:
```sql
UPDATE __migration_lock SET finished_on = NOW() WHERE finished_on IS NULL;
```

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
