$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$prueba = Join-Path ([IO.Path]::GetTempPath()) ('Organizer-' + [guid]::NewGuid().ToString('N'))
$exe = Join-Path $prueba 'Organizer.exe'
$origen = Join-Path $prueba 'A'
$otro = Join-Path $prueba 'B'
$destino = Join-Path $prueba 'Nueva'
$anio = Join-Path $origen 'Photos from 2019'
$fb = Join-Path $origen 'FACEBOOK'
$album = Join-Path $otro 'Mi album'
$ocupada = Join-Path $destino '2019\01 - enero\2019-01-17\Fotos'
foreach ($dir in @($anio,$fb,$album,$ocupada)) { [void][IO.Directory]::CreateDirectory($dir) }
$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /optimize+ /codepage:65001 /r:System.Web.Extensions.dll /r:Microsoft.VisualBasic.dll "/out:$exe" (Join-Path $raiz 'src\Organizador.cs') (Join-Path $raiz 'src\Fechas.cs') (Join-Path $raiz 'src\Program.cs')
if ($LASTEXITCODE) { throw 'Error de compilacion' }
Copy-Item -LiteralPath (Join-Path $raiz 'Organizer.exe.config') -Destination $prueba
function Media($dir,$name) { [IO.File]::WriteAllBytes((Join-Path $dir $name),[byte[]](1,2,3)) }
function Verificar($condicion,$detalle) { if (-not $condicion) { throw $detalle } }
Media $anio 'IMG_20190117_094526861.jpg'
Media $anio 'sin dato.jpg'
Media $anio 'Screenshot_20230606_com.facebook.katana.jpg'
Media $anio 'x.mov'
Media $anio '0123456789abcdef0123456789abcdef.mp4'
Media $fb '0123456789abcdef0123456789abcdef.mp4'
Media $anio 'otro.dat'
Media $otro 'IMG_20190117_094526861.jpg'
Media $album 'evento.jpg'
Media $album 'evento.xmp'
Media $fb 'abc.mp4'
Media $anio 'con json.jpg'
Media $ocupada 'IMG_20190117_094526861.jpg'
# A JPEG APP1 segment with a little-endian TIFF directory and DateTimeOriginal.
$tiff = [byte[]](73,73,42,0,8,0,0,0,1,0,105,135,4,0,1,0,0,0,26,0,0,0,0,0,0,0,
    1,0,3,144,2,0,20,0,0,0,44,0,0,0,0,0,0,0)
$exif = [byte[]]((69,120,105,102,0,0) + $tiff + [Text.Encoding]::ASCII.GetBytes('2012:02:29 23:59:00') + 0)
$jpeg = [byte[]]((255,216,255,225,0,($exif.Length+2)) + $exif + (255,217))
[IO.File]::WriteAllBytes((Join-Path $anio 'IMG_20200101.jpg'),$jpeg)
[IO.File]::WriteAllBytes((Join-Path $anio 'con json.jpg'),$jpeg)
[IO.File]::WriteAllText((Join-Path $anio 'con json.jpg.supplemental-metadata.json'),'{"title":"con json.jpg","photoTakenTime":{"timestamp":"1609502400"}}')
[IO.File]::WriteAllText((Join-Path $anio 'album.json'),'{"title":"fiesta"}')
& $exe prever $destino $origen $otro
Verificar ($LASTEXITCODE -eq 0) 'prever'
$lote = (Get-ChildItem -Directory -LiteralPath (Join-Path $prueba 'Planes')).FullName
$plan = @(Import-Csv -LiteralPath (Join-Path $lote 'Plan.csv'))
Verificar ($plan.Count -eq 13) 'cantidad de filas'
Verificar (@($plan | Where-Object Tipo -eq 'Videos\Posibles TikToks').Count -eq 1) 'video hexadecimal'
Verificar ($plan.Destino -like '*Videos\Facebook\0123456789abcdef0123456789abcdef.mp4') 'prioridad de Facebook'
Verificar ($plan.Destino -like '*2012-02-29\Fotos\IMG_20200101.jpg') 'EXIF tiene prioridad sobre el nombre'
Verificar ($plan.Destino -like '*2023-06-06\Capturas\Facebook\*') 'captura Facebook'
Verificar ($plan.Destino -like '*2019\Mes desconocido\Fotos\sin dato.jpg') 'solo anio'
Verificar ($plan.Destino -like '*Sin fecha\Videos\Facebook\abc.mp4') 'video Facebook'
Verificar ($plan.Destino -like '*Sin fecha\Fotos\Mi album\evento.xmp') 'album y adjunto XMP'
Verificar ($plan.Destino -like '*2021-01-01\Fotos\con json.jpg.supplemental-metadata.json') 'Takeout'
Verificar ($plan.Destino -like '*094526861 (3).jpg') 'colisiones'
Verificar (@($plan | Where-Object { -not [IO.File]::Exists($_.Origen) }).Count -eq 0) 'la vista previa altero el origen'
& $exe aplicar $lote
Verificar ($LASTEXITCODE -eq 0) 'aplicar'
Verificar (@($plan | Where-Object { [IO.File]::Exists($_.Origen) -or -not [IO.File]::Exists($_.Destino) }).Count -eq 0) 'movimiento'
& $exe aplicar $lote
Verificar ($LASTEXITCODE -eq 0) 'repetir'
# Simulate interruption after the rename and before its final journal entry.
$log = Join-Path $lote 'Movimientos.log'
$lines = [IO.File]::ReadAllLines($log)
[IO.File]::WriteAllLines($log,$lines[0..($lines.Length-2)],[Text.Encoding]::ASCII)
& $exe deshacer $lote
Verificar ($LASTEXITCODE -eq 0) 'deshacer despues de una interrupcion'
Verificar (@($plan | Where-Object { -not [IO.File]::Exists($_.Origen) -or [IO.File]::Exists($_.Destino) }).Count -eq 0) 'revertir'
[IO.File]::WriteAllText($plan[0].Destino,'ocupado')
& $exe aplicar $lote
Verificar ($LASTEXITCODE -eq 2) 'detectar destino ocupado'
Verificar (([IO.File]::ReadAllText($plan[0].Destino) -eq 'ocupado') -and [IO.File]::Exists($plan[0].Origen)) 'no sobrescribir'
& $exe deshacer $lote
Verificar ($LASTEXITCODE -eq 0) 'deshacer lote parcial'
[IO.File]::AppendAllText((Join-Path $lote 'Plan.csv'),'cambio')
& $exe aplicar $lote
Verificar ($LASTEXITCODE -eq 1) 'plan modificado'
# Drive the English wizard, then apply and undo its plan in Spanish.
$wizardOutput = @('1', '1', $anio, '', $prueba, 'My Photo Library', '', '4') | & $exe
Verificar ($LASTEXITCODE -eq 0) 'English wizard failed'
$englishBatch = Get-ChildItem -Directory -LiteralPath (Join-Path $prueba 'Planes') | Where-Object FullName -ne $lote
$english = @(Import-Csv -LiteralPath (Join-Path $englishBatch.FullName 'Plan.csv'))
Verificar ($english.Destination -like '*My Photo Library\2012\02 - February\2012-02-29\Photos\*') 'English date and library name'
Verificar ($english.Destination -like '*Unknown month\Videos\Possible TikToks\*') 'English category'
Verificar ($english.Type -contains 'Sidecar') 'English sidecar'
& $exe --lang es aplicar $englishBatch.FullName
Verificar ($LASTEXITCODE -eq 0) 'apply English plan in Spanish'
& $exe --lang en undo $englishBatch.FullName
Verificar ($LASTEXITCODE -eq 0) 'undo English plan'
$invalidOutput = @('2','1',$anio,'',$prueba,'..\Otro','4') | & $exe
Verificar (@(Get-ChildItem -Directory -LiteralPath (Join-Path $prueba 'Planes')).Count -eq 2) 'invalid library name'
$library=Join-Path $prueba 'Sorted'
$folders=@('2024\06 - junio\2024-06-03\Capturas\Facebook\Album','2024\07 - July\2024-07-01\Capturas\Facebook\Album',
    '2024\Mes desconocido\Videos\Posibles TikToks','Sin fecha\Fotos','Falsos Positivos\12 - diciembre\1969-12-31\Videos\Facebook')
foreach ($folder in $folders) {
    $path=Join-Path $library $folder
    [void][IO.Directory]::CreateDirectory($path)
    if ($folder -like '*Videos*') { Media $path 'clip.mp4' } else { Media $path 'unknown.jpg' }
}
foreach ($mode in @('day','month','year','type')) {
    $outDir=Join-Path $prueba $mode
    & $exe prever --layout $mode $outDir $library | Out-Null
    Verificar ($LASTEXITCODE -eq 0) "preview $mode"
    $batch=Get-ChildItem -Directory -LiteralPath (Join-Path $prueba 'Planes') | Where-Object {
        (Get-Content -Raw -LiteralPath (Join-Path $_.FullName 'Lote.json') | ConvertFrom-Json).Destino -eq $outDir
    }
    $rows=@(Import-Csv -LiteralPath (Join-Path $batch.FullName 'Plan.csv'))
    $prefix=@{day='2024\06 - junio\2024-06-03\';month='2024\06 - junio\';year='2024\';type=''}[$mode]
    Verificar ($rows.Count -eq 5) "all files $mode"
    Verificar ($rows.Destino -contains (Join-Path $outDir ($prefix+'Capturas\Facebook\Album\unknown.jpg'))) "layout and album $mode"
    Verificar ($rows.Destino -like '*Falsos Positivos\*Videos\Facebook\clip.mp4') "manual folder $mode"
    if ($mode -eq 'year' -or $mode -eq 'type') {
        Verificar ($rows.Destino -like '*unknown (2).jpg') "merge collision $mode"
        Verificar ($rows.Destino -contains (Join-Path $outDir ($prefix+'Videos\Posibles TikToks\clip.mp4'))) "unknown month $mode"
    }
    & $exe aplicar $batch.FullName | Out-Null
    Verificar ($LASTEXITCODE -eq 0) "apply $mode"
    & $exe deshacer $batch.FullName | Out-Null
    Verificar ($LASTEXITCODE -eq 0) "undo $mode"
}
Write-Output "Pruebas correctas en $prueba"
