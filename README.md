# DbDeploy

This is a simple database migration tool that can be used to manage database schema changes.
It currently supports PostgreSQL and MSSQL.

## Configuration

The configuration can be done via command line arguments. The following arguments are available:

- `--command`: The command to execute. Possible values are `update`, `status` and `sync`.
- `--startingFile`: The starting file. This is a json file that contains the files to include.
- `--maxLockWait`: The maximum time to wait for the lock in seconds. Default is 120 seconds.
- `--contexts`: The contexts to use. Multiple contexts can be separated by a comma.
- `--provider`: The provider to use. Possible values are `postgres` and `mssql`.
- `--connectionString`: The connection string to use.
- `--connectionAttempts`: The number of initial connection attempts. Default is 10.
- `--connectionRetryDelay`: The delay between connection attempts in seconds. Default is 5 seconds.
- `--logLevel`: The log level to use. Possible values are `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`. Default is `Information`.

The root directory is `/Migrations`. This is the parent directory of the starting file and all the files that are included.

### Running the Container

The container is available on [Docker Hub](https://hub.docker.com/r/abeseler/dbdeploy).

You can use the command line arguments above or the following environment variables for configuration:

- `Deploy__Command`: The command to execute. Possible values are `update`, `status` and `sync`.
- `Deploy__StartingFile`: The starting file. This is a json file that contains the files to include.
- `Deploy__MaxLockWaitSeconds`: The maximum time to wait for the lock in seconds. Default is 120 seconds.
- `Deploy__Contexts`: The contexts to use. Multiple contexts can be separated by a comma.
- `Deploy__DatabaseProvider`: The provider to use. Possible values are `postgres` and `mssql`.
- `Deploy__ConnectionString`: The connection string to use.
- `Deploy__ConnectionAttempts`: The number of initial connection attempts. Default is 10.
- `Deploy__ConnectionRetryDelaySeconds`: The delay between connection attempts in seconds. Default is 5 seconds.
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
      "PostDeplayScripts"
    ],
    "contextFilter": [],
    "requiresContext": false,
    "errorIfMissingOrEmpty": true
  },
  {
    "include": [
      "SeedScripts"
    ],
    "contextFilter": ["seed"],
    "requiresContext": true,
    "errorIfMissingOrEmpty": false
  }
]
```

The following properties are available:

- `include`: The files or directories to include.
- `contextFilter`: The contexts to use. If the context is not provided, the includes will be used for all contexts.
- `requiresContext`: If the context is required. Default is `false`.
- `errorIfMissingOrEmpty`: If an error should be thrown if the included file or directory is missing or empty. Default is `true`.

Migrations are executed in the order they are included in the starting file. If a directory is included, the files are executed in alphabetical order.

DbDeploy is not opinionated about how you organize your migrations. However, generally I prefer to have 1 folder per type of object (Tables, Views, Stored Procedures, etc.) and then 1 file per object. This makes it easier to manage and track changes.
