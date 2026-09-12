param([string]$Blender='C:\Program Files\Blender Foundation\Blender 3.6\blender.exe')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixture=Join-Path $root ('Temp/RetargetPipeline-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
& $Blender --background --factory-startup --python-exit-code 1 --python (Join-Path $PSScriptRoot 'TestRetargetCharacter.py')
if ($LASTEXITCODE -ne 0) { throw 'Retarget evaluation checks failed.' }
dotnet run --project (Join-Path $root 'Tools/AssetPacker') -- extract (Join-Path $root 'Assets/StreamingAssets/SF2Content/ArtBundles/MODELS.tar.lz4') (Join-Path $fixture 'Core')
if ($LASTEXITCODE -ne 0) { throw 'Canonical extraction failed.' }
$rig=Join-Path $fixture 'Core/models/mdl_skeleton.xml'
& $Blender --background --factory-startup --python-exit-code 1 --python (Join-Path $PSScriptRoot 'CreateRetargetFixture.py') -- --rig $rig --output $fixture
if ($LASTEXITCODE -ne 0) { throw 'Donor fixture creation failed.' }
$output=Join-Path $fixture 'Retargeted'
& $Blender --background --factory-startup (Join-Path $fixture 'donor.blend') --python-exit-code 1 --python (Join-Path $PSScriptRoot 'RetargetCharacter.py') -- --rig $rig --mapping (Join-Path $fixture 'bindings.json') --armature Donor --reference-frame 1 --start 1 --end 31 --output $output
if ($LASTEXITCODE -ne 0) { throw 'Canonical retarget CLI failed.' }
$clip=Join-Path $output 'retargeted.bytes'
python (Join-Path $PSScriptRoot 'CharacterPipeline.py') validate --rig $rig --animation $clip
if ($LASTEXITCODE -ne 0) { throw 'Retarget payload/sidecar validation failed.' }
$package=Join-Path $fixture 'local.retarget-preview'
python (Join-Path $PSScriptRoot 'PackageCharacter.py') --rig $rig --animation $clip --skin (Join-Path $fixture 'skin.xml') --mod-id local.retarget-preview --output $package
if ($LASTEXITCODE -ne 0) { throw 'Retarget packaging failed.' }
& (Join-Path $PSScriptRoot 'TestCharacterLua.ps1') -Package $package -Packaged
& (Join-Path $PSScriptRoot 'TestSf2Animation.ps1') -Animation $clip -ExpectedFrames 61 -ExpectedNodes 67
Write-Output "Retarget integration artifacts: $fixture"
