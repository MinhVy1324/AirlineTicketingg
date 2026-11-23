[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$EnvFile = (Join-Path $PSScriptRoot '..\.env'),
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [switch]$RestoreDrill
)

$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath $EnvFile | ConvertFrom-StringData
if (!$config.POSTGRES_DB -or !$config.POSTGRES_USER -or !$config.POSTGRES_PASSWORD -or !$config.POSTGRES_PORT) {
    throw 'Cần POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD và POSTGRES_PORT.'
}
$sourceDatabase = $config.POSTGRES_DB
$directory = New-Item -ItemType Directory -Path $OutputDirectory -Force
$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd_HHmmss')
$dumpFile = Join-Path $directory.FullName "airline_${stamp}_$([Guid]::NewGuid().ToString('N').Substring(0, 8)).dump"
$connection = @('-h', '127.0.0.1', '-p', $config.POSTGRES_PORT, '-U', $config.POSTGRES_USER)
$oldPgPassword = $env:PGPASSWORD

function Invoke-Postgres([string]$Program, [string[]]$Arguments) {
    & (Join-Path $PostgresBin "$Program.exe") @connection @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program thất bại (mã $LASTEXITCODE)." }
}

try {
    $env:PGPASSWORD = $config.POSTGRES_PASSWORD
    Invoke-Postgres 'pg_dump' @('-Fc', '-d', $sourceDatabase, '-f', $dumpFile)
    $result = [ordered]@{
        Source = $sourceDatabase
        Backup = $dumpFile
        Bytes = (Get-Item -LiteralPath $dumpFile).Length
        Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dumpFile).Hash
    }

    if ($RestoreDrill) {
        $targetDatabase = "airline_restore_${stamp}_$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
        if ($targetDatabase -eq $sourceDatabase) { throw 'CSDL khôi phục phải khác CSDL nguồn.' }
        # createdb fails if the target exists. No drop or clean is used.
        Invoke-Postgres 'createdb' @('--template=template0', $targetDatabase)
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        Invoke-Postgres 'pg_restore' @('--exit-on-error', '--single-transaction', '--no-owner',
            '--no-privileges', '-d', $targetDatabase, $dumpFile)
        $elapsed.Stop()
        $counts = @'
SELECT 'DatVes', count(*) FROM "DatVes"
UNION ALL SELECT 'Ves', count(*) FROM "Ves"
UNION ALL SELECT 'ThanhToans', count(*) FROM "ThanhToans"
UNION ALL SELECT 'HoanTiens', count(*) FROM "HoanTiens"
UNION ALL SELECT 'TaiKhoans', count(*) FROM "TaiKhoans"
UNION ALL SELECT 'EmailDeliveryJobs', count(*) FROM "EmailDeliveryJobs"
UNION ALL SELECT '__EFMigrationsHistory', count(*) FROM "__EFMigrationsHistory"
ORDER BY 1;
'@
        $original = @(Invoke-Postgres 'psql' @('-X', '-At', '-v', 'ON_ERROR_STOP=1', '-d', $sourceDatabase, '-c', $counts))
        $restored = @(Invoke-Postgres 'psql' @('-X', '-At', '-v', 'ON_ERROR_STOP=1', '-d', $targetDatabase, '-c', $counts))
        if (Compare-Object $original $restored) {
            throw "Số bản ghi khác nguồn. Nếu nguồn có ghi đồng thời, thử lại trong cửa sổ yên tải. Bản khôi phục nằm ở $targetDatabase."
        }
        $result.RestoredDatabase = $targetDatabase
        $result.RestoreSeconds = [Math]::Round($elapsed.Elapsed.TotalSeconds, 2)
        $result.VerifiedCounts = $restored
    }
    [pscustomobject]$result
}
finally {
    $env:PGPASSWORD = $oldPgPassword
}
