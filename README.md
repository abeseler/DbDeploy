# DbDeploy

This is a simple database migration tool that can be used to manage database schema changes.
It currently supports PostgreSQL and MSSQL.

## Container

The container is available on [Docker Hub](https://hub.docker.com/r/abeseler/dbdeploy).

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
- `--logLevel`: The log level to use. Possible values are `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, and `None`. Default is `Information`.

## Starting File

The starting file is a json file that contains an array of includes. The following is an example of a starting file:
```json
[
  {
    "include": [
      "init.sql",
      "Tables",
      "Views"
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
