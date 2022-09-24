$d = get-date
$mainVersion = [String]::Format("{0}.{1}", 1, (Get-Date).Year - 2000)

function get-DynamicVersion {
    # Minor revsion = total hours since year start
    $hoursThisYear = ((Get-Date) - (Get-Date -Day 1 -Month 1 -Year  (Get-Date).Year -Hour 0 -Minute 0 -Second 0)).TotalHours
    $minute = (Get-Date).Minute
    $revision = [int][Math]::Round($minute,0)
    $majorVersion = [int][Math]::Floor($hoursThisYear)
    return "$majorVersion.$revision"
}

function replace-AssemblyVersion($assemblyFile)
{
    $oldContent = Get-Content $assemblyFile
    
    $dynamicVersion = get-DynamicVersion
    
    $newVersion = $mainVersion + "." + $dynamicVersion
    Write-Host -ForegroundColor Yellow "$assemblyFile set version $newVersion" 
    
    
    $newContent = $oldContent | ?{$_ -notmatch "Assembly[a-zA-Z]+Version"}
    $lineToReplace = $newContent | ?{$_ -match "Assembly+Version" -And $_ -notmatch "//"}
    
    $newContent = $newContent.Replace($lineToReplace, "[assembly: AssemblyVersion(`"$newVersion`")]`r`n[assembly: AssemblyFileVersion(`"$newVersion`")]`r`n[assembly: AssemblyInformationalVersion(`"$newVersion`")]")
    if($newContent -ne $null -and $newContent.Length -gt 10) {
        Set-Content -Path $assemblyFile -Value $newContent -Encoding UTF8 
    } else {
        Write-Host -ForegroundColor Red "Fehler! Bitte wiederholen."
    }
}

function update-AssemblyVersion($projectFolder, $onlyIfChanged = $false) {
    if($onlyIfChanged){
        
        # get timestamp from AssemblyInfo.cs file
        $lastVersionUpdate = (get-childitem -Recurse -Path $projectFolder AssemblyInfo.cs | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        # get newest *.cs files
        $lastUpdate = (get-childitem -Recurse -Path $projectFolder -Include "*.cs", "*.csproj", "*.sln", "*.json", "*.config", "*.manifest", "*.dat", "*.xaml" -Exclude "*.g.cs", "*.g.i.cs" | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        # check if newer
        if($lastVersionUpdate -lt $lastUpdate){
            get-childitem -Recurse -Path $projectFolder AssemblyInfo.cs | %{replace-AssemblyVersion $_.FullName}
        } else {
            Write-Host -ForegroundColor Green "No update found in $projectFolder ($lastVersionUpdate >= $lastUpdate)"
        }

    } else {
        get-childitem -Recurse -Path $projectFolder AssemblyInfo.cs | %{replace-AssemblyVersion $_.FullName}
    }
}


function update-WIXVersion($productWIXFile) {
    $versionRegex = "Version=`"[0-9]+`.[0-9]+`.[0-9]+`.[0-9]+`"";
    $wixContent = Get-Content $productWIXFile
    $match = select-string $versionRegex -inputobject $wixContent
    
    $dynamicVersion = get-DynamicVersion
    $newVersion = $mainVersion + "." + $dynamicVersion
    $newVersionString = "Version=`"$newVersion`""

    $oldVersion = $match.Matches.Groups[0].Value
    
    if($oldVersion -ne $newVersionString){
        "WIX Version $oldVersion => $newVersionString"
        $newWIXContent = $wixContent.Replace($oldVersion, $newVersionString)
        Set-Content -Path $productWIXFile -Value $newWIXContent
    }
}


function increase-ApplicationVersion($projectFile) {
    
    $versionRegex = "<ApplicationVersion>[0-9]+`.[0-9]+`.[0-9]+`.[0-9]+</ApplicationVersion>";
    $projectFileContent = Get-Content $projectFile
    $match = select-string $versionRegex -inputobject $projectFileContent
    
    $dynamicVersion = get-DynamicVersion
    $newVersion = $mainVersion + "." + $dynamicVersion
    $newVersionString = "<ApplicationVersion>$newVersion</ApplicationVersion>"

    $oldVersion = $match.Matches.Groups[0].Value
    
    if($oldVersion -ne $newVersionString){
        "ProjectFile ApplicationVersion $oldVersion => $newVersionString"
        $newProjectFileContent = $projectFileContent.Replace($oldVersion, $newVersionString)
        Set-Content -Path $projectFile -Value $newProjectFileContent
    }
}

