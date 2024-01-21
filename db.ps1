Set-Alias -Name db -Value Invoke-DbAction
function Invoke-DbAction {
	param (
		[Parameter(Mandatory = $true)]
		[string]$action,
		[Parameter(Mandatory = $false)]
		[string]$database
	)
	if ($null -eq $env:DEVDRIVE) {
        Write-Error "Missing environment variable: DEVDRIVE"
        return
	}
	if ($action -eq "start") {
		docker compose -f "$env:DEVDRIVE\DbDeploy\docker-compose.yaml" up -d
		return
	}
	if ($action -eq "stop") {
		docker compose -f "$env:DEVDRIVE\DbDeploy\docker-compose.yaml" down
		return
	}
	if ($action -eq "reset") {
		docker compose -f "$env:DEVDRIVE\DbDeploy\docker-compose.yaml" down -v
		return
	}
	if ($action -eq "build"){
		docker build -t dbdeploy:latest -f "$env:DEVDRIVE\DbDeploy\src\DbDeploy\Dockerfile" "$env:DEVDRIVE\DbDeploy"
		return
	}
	if ($action -eq "update" -or $action -eq "sync" -or $action -eq "status") {
		$container = docker ps --format "{{.Names}}" | Where-Object { $_ -eq "local-mssql" }
		if ($null -eq $container) {
			Invoke-DbAction start
		}
		$dockerImage = docker images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -eq "dbdeploy:latest" }
		if ($null -eq $dockerImage) {
			Invoke-DbAction build
		}
		$dbPath = "$env:DEVDRIVE\$database.DB\"
		if (Test-Path $dbPath) {
			docker run --rm --name init-db-$database --network local-net --entrypoint /opt/mssql-tools/bin/sqlcmd -w /scripts -v $dbPath\:/scripts mcr.microsoft.com/mssql-tools -S local-mssql -U sa -P P@ssw0rd -i bootstrap-database.sql
			if ($LASTEXITCODE -ne 0) {
				Write-Error "SQL Server is not ready yet. Please try again in a few seconds."
				return
			}
			docker run --rm --name deploy-db-$database --network local-net --volume $dbPath\src:/app/Migrations:ro --env-file $dbPath\local.env dbdeploy:latest --command $action
			return
		}
		Write-Error "Invalid database: $database"
		return
	}
	Write-Host "Invalid command"
}
Register-ArgumentCompleter -CommandName Invoke-DbAction -ParameterName action -ScriptBlock {
	param($commandName, $wordToComplete, $cursorPosition)
	@("update", "sync", "status", "start", "stop", "reset", "build")
}
Register-ArgumentCompleter -CommandName Invoke-DbAction -ParameterName database -ScriptBlock {
	param($commandName, $wordToComplete, $cursorPosition)
	@("Beseler")
}