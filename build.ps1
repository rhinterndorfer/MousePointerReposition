$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

# generate version
. $PSScriptRoot\generateNewVersion.ps1


# clean output directories
if(Test-Path "$PSScriptRoot\MousePointerReposition\bin\Release") { Remove-Item -Recurse -Force "$PSScriptRoot\MousePointerReposition\bin\Release" }
if(Test-Path "$PSScriptRoot\MousePointerReposition\bin\Debug") { Remove-Item -Recurse -Force "$PSScriptRoot\MousePointerReposition\bin\Debug" }
if(Test-Path "$PSScriptRoot\Publish") { Remove-Item -Recurse -Force "$PSScriptRoot\Publish\*" }


# build VSTO project
Write-Host "=========================================="
Write-Host "build MousePointerReposition project"
Write-Host "AnyCPU"
. $msbuild "$PSScriptRoot\MousePointerReposition\MousePointerReposition.csproj" /nologo /clp:"ErrorsOnly;Summary" /verbosity:quiet /target:Clean /property:Platform="AnyCPU" /property:Configuration="Release" /property:DebugSymbols="false" /property:DebugType="none" /t:Rebuild /property:OutputPath="$PSScriptRoot\MousePointerReposition\bin\Release"

# cleanup (xml documentation and dbg symbols)
Write-Host "=========================================="
Write-Host "Cleanup documentation (xml) and debug symbols (pdb) of external libraries (NUGET)"
foreach($lib in Get-ChildItem "$PSScriptRoot\MousePointerReposition\bin\Release\*.dll") 
{
    $xmlFile = $lib.FullName.Replace(".dll",".xml")
    $pdbFile = $lib.FullName.Replace(".dll",".pdb")
    if(Test-Path $pdbFile) {
        Write-Host "Cleanup $pdbFile"
        Remove-Item -Path $pdbFile -Force
    }
    if(Test-Path $xmlFile) {
        Write-Host "Cleanup $xmlFile"
        Remove-Item -Path $xmlFile -Force
    }
}


# compress
if(-Not (Test-Path "$PSScriptRoot\Publish")){
    New-Item "$PSScriptRoot\Publish" -ItemType Directory
}
Compress-Archive -Path "$PSScriptRoot\MousePointerReposition\bin\Release\*" -DestinationPath "$PSScriptRoot\Publish\MousePointerReposition.zip"
