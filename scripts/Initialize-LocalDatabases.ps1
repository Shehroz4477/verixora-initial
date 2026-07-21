[CmdletBinding()]
param(
    [ValidateSet('PostgreSql', 'SqlServer', 'All')]
    [string]$Provider = 'All'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Assert-ContainerRunning {
    param([Parameter(Mandatory)][string]$ContainerName)

    $state = (& docker inspect --format '{{.State.Running}}' $ContainerName 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or $state -ne 'true') {
        throw "Required local container '$ContainerName' is not running. Start infrastructure/docker-compose.yml first."
    }
}

function Invoke-SqlServerScript {
    param([Parameter(Mandatory)][string]$Sql, [Parameter(Mandatory)][string]$Database)

    # Run SQLCMD directly with each batch as an argument. This avoids both the UTF-8
    # BOM Windows PowerShell adds to native stdin and PowerShell's shell-command
    # parsing differences. SQLCMDPASSWORD is configured inside the local container.
    # Splitting on SQLCMD batch separators keeps CREATE/ALTER PROCEDURE statements valid.
    $batches = $Sql -split '(?im)^[ \t]*go[ \t]*(?:--.*)?\r?$'
    foreach ($batch in $batches) {
        if ([string]::IsNullOrWhiteSpace($batch)) { continue }

        $arguments = @(
            'exec',
            'verixora-sqlserver',
            '/opt/mssql-tools18/bin/sqlcmd',
            '-C', '-b',
            '-S', 'localhost',
            '-U', 'sa',
            '-d', $Database,
            '-Q', $batch)
        & docker @arguments | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "SQL Server schema execution failed for database '$Database'." }
    }
}

function Initialize-SqlServer {
    Assert-ContainerRunning 'verixora-sqlserver'
    Invoke-SqlServerScript -Database 'master' -Sql "if db_id(N'verixora') is null create database [verixora];"

    $files = @(Get-ChildItem (Join-Path $projectRoot 'database\sqlserver') -Filter '*.sql' | Sort-Object Name)
    foreach ($file in $files) {
        Invoke-SqlServerScript -Database 'verixora' -Sql (Get-Content -Raw $file.FullName)
    }
    return $files.Name
}

function Invoke-PostgreSqlScript {
    param([Parameter(Mandatory)][string]$Sql)

    $Sql | docker exec -i verixora-postgres /bin/sh -c 'PGOPTIONS="--client-min-messages=warning" PGPASSWORD="$POSTGRES_PASSWORD" psql -q -v ON_ERROR_STOP=1 -h localhost -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f -' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL schema execution failed.' }
}

function Get-PostgreSqlScalar {
    param([Parameter(Mandatory)][string]$Sql)

    $output = $Sql | docker exec -i verixora-postgres /bin/sh -c 'PGOPTIONS="--client-min-messages=warning" PGPASSWORD="$POSTGRES_PASSWORD" psql -X -qAt -v ON_ERROR_STOP=1 -h localhost -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f -'
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL schema inspection failed.' }
    return ($output | Out-String).Trim()
}

function Register-PostgreSqlMigration {
    param([Parameter(Mandatory)][string]$ScriptName)

    $escapedName = $ScriptName.Replace("'", "''")
    Invoke-PostgreSqlScript -Sql "insert into public.verixora_schema_migrations (script_name) values ('$escapedName') on conflict (script_name) do nothing;"
}

function Initialize-PostgreSql {
    Assert-ContainerRunning 'verixora-postgres'

    $files = @(Get-ChildItem (Join-Path $projectRoot 'database\postgresql') -Filter '*.sql' | Sort-Object Name)
    Invoke-PostgreSqlScript -Sql @'
create table if not exists public.verixora_schema_migrations
(
    script_name varchar(255) primary key,
    applied_at_utc timestamptz not null default now()
);
'@

    $migrationCount = [int](Get-PostgreSqlScalar -Sql 'select count(*) from public.verixora_schema_migrations;')
    if ($migrationCount -eq 0) {
        $hasExistingApplicationSchema = Get-PostgreSqlScalar -Sql @'
select exists(
    select 1 from pg_namespace
    where nspname in ('homes', 'identity', 'devices', 'smartlocks', 'auditlogs'));
'@
        if ($hasExistingApplicationSchema -eq 't') {
            # Existing local databases created before this runner have no history table.
            # Baseline only when the current security-critical schema is already present;
            # a partial schema is unsafe to guess and must be recreated or migrated manually.
            $isLatestCompatibleSchema = Get-PostgreSqlScalar -Sql @'
select
    to_regclass('identity.users') is not null
    and to_regclass('identity.trusted_devices') is not null
    and to_regclass('smartlocks.lock_commands') is not null
    and exists (
        select 1 from information_schema.columns
        where table_schema = 'identity'
          and table_name = 'trusted_devices'
          and column_name = 'device_public_key_spki_base64')
    and exists (
        select 1 from pg_proc p join pg_namespace n on n.oid = p.pronamespace
        where n.nspname = 'identity' and p.proname = 'fn_trusted_device_id_exists')
    and exists (
        select 1 from pg_proc p join pg_namespace n on n.oid = p.pronamespace
        where n.nspname = 'smartlocks' and p.proname = 'fn_acknowledge_lock_command');
'@
            if ($isLatestCompatibleSchema -ne 't') {
                throw 'The existing PostgreSQL schema is partial or older than this project. Restore a backup or recreate the local development database before using the migration runner.'
            }

            foreach ($file in $files) { Register-PostgreSqlMigration -ScriptName $file.Name }
            return $files.Name
        }
    }

    $applied = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $files) {
        $escapedName = $file.Name.Replace("'", "''")
        $alreadyApplied = Get-PostgreSqlScalar -Sql "select exists(select 1 from public.verixora_schema_migrations where script_name = '$escapedName');"
        if ($alreadyApplied -eq 't') { continue }
        Invoke-PostgreSqlScript -Sql (Get-Content -Raw $file.FullName)
        Register-PostgreSqlMigration -ScriptName $file.Name
        $applied.Add($file.Name)
    }
    return $applied
}

$result = [ordered]@{}
if ($Provider -in @('PostgreSql', 'All')) {
    $result['PostgreSql'] = @(Initialize-PostgreSql)
}
if ($Provider -in @('SqlServer', 'All')) {
    $result['SqlServer'] = @(Initialize-SqlServer)
}

[pscustomobject]@{
    Provider = $Provider
    PostgreSqlScripts = @($result['PostgreSql']).Count
    SqlServerScripts = @($result['SqlServer']).Count
    Success = $true
} | ConvertTo-Json -Compress
