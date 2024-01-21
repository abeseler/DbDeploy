# DbDeploy
 
## Powershell Profile Helpers

Setup PowerShell profile functions and aliases to simplify CLI commands by adding db.ps1 to your PowerShell profile.

- `db start`
	- Starts the DB compose project
- `db stop`
	- Stops the DB compose project
- `db reset`
	- Tears down the DB project and cleans up all resources
- `db build`
	- Builds the dbdeploy container
- `db {command} DatabaseName`
	- Available commands:
		- `update` - Applies new and changed migrations
		- `sync` - Marks all unapplied changes as applied
		- `status` - Show a summary of what running update will apply and sync
	- This command will execute *db start* and *db build* if necessary
