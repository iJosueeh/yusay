$composeFile = "C:\Users\IJosueeh\Documents\Proyectos\yusay\docker-compose.yml"

# Setup test instrument and version in READY status
$setupSql = @"
INSERT INTO yusay.instrument (instrument_id, code, name, description, purpose)
VALUES ('77777777-7777-4777-8777-777777777777', 'INST_CONCURR_TEST', 'Concurr Test', 'Desc', 'Purpose')
ON CONFLICT (code) DO NOTHING;

INSERT INTO yusay.instrument_version (
    instrument_id, instrument_version_id, version, status,
    source_description, population, limitations
) VALUES (
    '77777777-7777-4777-8777-777777777777',
    '88888888-8888-4888-8888-888888888888',
    1, 'READY', 'Src', 'Pop', 'Lim'
)
ON CONFLICT DO NOTHING;
"@
$setupSql | docker compose -f $composeFile exec -T postgres psql -U postgres -d yusay

Write-Host "Iniciando Transacción 1 (Edición con FOR SHARE y espera de 3s)..."
$job1 = Start-Job -ArgumentList $composeFile -ScriptBlock {
    param($cfg)
    $t1 = @"
BEGIN;
INSERT INTO yusay.question (question_id, instrument_version_id, position, prompt, required)
VALUES ('99999999-9999-4999-8999-999999999999', '88888888-8888-4888-8888-888888888888', 1, 'Pregunta Concurrente', true);
SELECT pg_sleep(3);
COMMIT;
"@
    $start = Get-Date
    $res = $t1 | docker compose -f $cfg exec -T postgres psql -U postgres -d yusay
    $duration = (Get-Date) - $start
    [PSCustomObject]@{ Task = "Tx1_Edit"; DurationSec = $duration.TotalSeconds; Output = ($res -join " ") }
}

Start-Sleep -Milliseconds 600

Write-Host "Iniciando Transacción 2 (Intento de publicación en paralelo)..."
$job2 = Start-Job -ArgumentList $composeFile -ScriptBlock {
    param($cfg)
    $t2 = @"
UPDATE yusay.instrument_version
SET status = 'PUBLISHED'
WHERE instrument_version_id = '88888888-8888-4888-8888-888888888888';
"@
    $start = Get-Date
    $res = $t2 | docker compose -f $cfg exec -T postgres psql -U postgres -d yusay
    $duration = (Get-Date) - $start
    [PSCustomObject]@{ Task = "Tx2_Publish"; DurationSec = $duration.TotalSeconds; Output = ($res -join " ") }
}

$res1 = Receive-Job -Job $job1 -Wait
$res2 = Receive-Job -Job $job2 -Wait

Write-Host "Tx1 Duration: $($res1.DurationSec)s, Output: $($res1.Output)"
Write-Host "Tx2 Duration: $($res2.DurationSec)s, Output: $($res2.Output)"

# Cleanup
$cleanupSql = @"
BEGIN;
-- Desbloquear temporalmente borrando la pregunta con trigger
DELETE FROM yusay.question WHERE instrument_version_id = '88888888-8888-4888-8888-888888888888';
DELETE FROM yusay.instrument_version WHERE instrument_version_id = '88888888-8888-4888-8888-888888888888';
DELETE FROM yusay.instrument WHERE instrument_id = '77777777-7777-4777-8777-777777777777';
COMMIT;
"@
# Como status terminó en PUBLISHED, el delete de question en cleanup requiere volver a DRAFT/READY antes o borrar en cascada
$cleanReset = @"
UPDATE yusay.instrument_version SET status = 'READY' WHERE instrument_version_id = '88888888-8888-4888-8888-888888888888';
UPDATE yusay.instrument_version SET status = 'DRAFT' WHERE instrument_version_id = '88888888-8888-4888-8888-888888888888';
DELETE FROM yusay.question WHERE instrument_version_id = '88888888-8888-4888-8888-888888888888';
DELETE FROM yusay.instrument_version WHERE instrument_version_id = '88888888-8888-4888-8888-888888888888';
DELETE FROM yusay.instrument WHERE instrument_id = '77777777-7777-4777-8777-777777777777';
"@
$cleanReset | docker compose -f $composeFile exec -T postgres psql -U postgres -d yusay
