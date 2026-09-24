param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../anadromo'))
$outputDir = Join-Path $project 'Temp/LocomotionValidation'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$unityData = Join-Path $UnityEditor 'Data'
$framework = Join-Path $unityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$engine = Join-Path $unityData 'Managed/UnityEngine/UnityEngine.CoreModule.dll'
$argsList = @('/nologo', '/nostdlib+', '/target:exe', '/langversion:9.0',
    ('/out:' + (Join-Path $outputDir 'CycleChecks.exe')),
    ('/r:' + (Join-Path $framework 'mscorlib.dll')),
    ('/r:' + (Join-Path $framework 'System.dll')),
    ('/r:' + (Join-Path $framework 'Facades/netstandard.dll')),
    ('/r:' + $engine),
    (Join-Path $project 'Assets/_Project/Scripts/Locomotion/ArmStrokeCycle.cs'),
    (Join-Path $PSScriptRoot 'ArmStrokeCycleChecks.cs'))
& (Join-Path $unityData 'NetCoreRuntime/dotnet.exe') (Join-Path $unityData 'DotNetSdkRoslyn/csc.dll') @argsList
if ($LASTEXITCODE -ne 0) { throw 'Cycle checks did not compile.' }
Copy-Item -LiteralPath $engine -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $unityData 'Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll') -Destination $outputDir -Force
& (Join-Path $unityData 'MonoBleedingEdge/bin/mono.exe') (Join-Path $outputDir 'CycleChecks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Cycle checks failed.' }
