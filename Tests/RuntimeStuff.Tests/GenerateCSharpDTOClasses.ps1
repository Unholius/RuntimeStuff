param(
    [string]$Server = "NAS\RSSQLSERVER",
    [string]$Database = "test",
    [string]$OutputFolder = ".\DTO",
    [string]$SqlFile = ".\GenerateCSharpDTOClasses.sql"
)

Write-Host "Generating DTO files..." -ForegroundColor Cyan

# Проверяем файл SQL
if (-not (Test-Path $SqlFile)) {
    Write-Host "SQL file not found: $SqlFile" -ForegroundColor Red
    exit
}

# Читаем SQL из файла
$sql = Get-Content -Path $SqlFile -Raw

# Создаём папку, если её нет
$OutputFolder = Join-Path -Path $PSScriptRoot -ChildPath $OutputFolder
if (-not (Test-Path $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder | Out-Null
}

# Строка подключения
$connectionString = "Server=$Server;Database=$Database;Integrated Security=True;"

# Подключаем ADO.NET
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
$command = $connection.CreateCommand()
$command.CommandText = $sql

try {
    $connection.Open()
    $reader = $command.ExecuteReader()

    if (-not $reader.HasRows) {
        Write-Host "SQL script returned no results!" -ForegroundColor Red
        $reader.Close()
        $connection.Close()
        exit
    }

    while ($reader.Read()) {

        $fileName = $reader["FILE_NAME"]
        $fileContent = $reader["FILE_CONTENT"]  # Полное значение, не обрезается

        $filePath = Join-Path $OutputFolder $fileName

        Write-Host "Creating file: $filePath"

        # Запись UTF-8 без BOM
        [System.IO.File]::WriteAllText($filePath, $fileContent, (New-Object System.Text.UTF8Encoding($false)))
    }

    $reader.Close()
}
finally {
    $connection.Close()
}

Write-Host "DONE! Files generated in $OutputFolder" -ForegroundColor Green