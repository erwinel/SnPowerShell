Param(
    [ValidateSet('Debug', 'Release')]
    # Build configuration.
    [string]$Configuration = 'Debug',

    [ValidateSet('Any CPU', 'x64', 'x86')]
    # Target platform.
    [string]$Platform = 'Any CPU',

    [ValidateSet('Build', 'Resources', 'Compile', 'Rebuild', 'Clean', 'Publish')]
    # Build targets.
    [string[]]$Target = @('Build'),

    [ValidateSet('Test', 'Deploy', 'None')]
    # Action to take after successful build.
    [string]$Action = 'None',

    [ValidateScript({ ((($PSScriptRoot | Join-Path -ChildPath 'src') | Join-Path -ChildPath $_) | Join-Path -ChildPath "$_.*proj") | Test-Path -PathType Leaf })]
    # Individual project to build (assumes project file path is "src/$Project/$Project.csproj", relative to the location of this script). If omitted, all projects in src/SnPowerShell.sln will be built.
    [string[]]$Project,

    # Minimum output buffer width in characters. This is typically to allow build output parsers, such as certain VS Code plugins, to accurately analyze build output by minimizing unexpected line breaks.
    [int]$MinOutBufferWidth = 2048,

    # Minimum output buffer height in characters (to allow sufficient build result buffering).
    [int]$MinOutBufferHeight = 6000,

    [ValidateSet('Maximize', 'MaxWidth', 'None')]
    # Determines how console is resized. Maximize: Adjusts console to maximum working space area; MaxWidth: Change width of console to width of output buffer; None: Do not attempt tochange console size.
    $ConsoleOptimize = 'None',

    [ValidateScript({ $_ | Test-Path -PathType Container })]
    # Location of MSBuild binaries.
    [string]$MsBuildBinPath = 'C:\Program Files (x86)\MSBuild\14.0\Bin'
)

Function Optimize-PsHostBufferSize {
    Param(
        # Minimum output buffer width.
        [int]$MinOutBufferWidth = 2048,
    
        # Minimum output buffer height.
        [int]$MinOutBufferHeight = 6000,
    
        [ValidateSet('Maximize', 'MaxWidth', 'None')]
        # Determines how console is resized.
        $ConsoleOptimize = 'None'
    )
    $CurrentHost = Get-Host -ErrorAction Stop;

    $Expected = $Actual = $CurrentHost.UI.RawUI.BufferSize;
    if ($Expected.Width -lt $MinOutBufferWidth) {
        if ($Expected.Height -lt $MinOutBufferHeight) {
            $CurrentHost.UI.RawUI.BufferSize = $Expected = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $MinOutBufferWidth, $MinOutBufferHeight;
        } else {
            $CurrentHost.UI.RawUI.BufferSize = $Expected = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $MinOutBufferWidth, $Expected.Height;
        }
    } else {
        if ($Expected.Height -lt $MinOutBufferHeight) {
            $CurrentHost.UI.RawUI.BufferSize = $Expected = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $Expected.Width, $MinOutBufferHeight;
        }
    }
    if ($Expected.Width -ne $CurrentHost.UI.RawUI.BufferSize.Width) {
        if ($Expected.Height -ne $CurrentHost.UI.RawUI.BufferSize.Height) {
            Write-Warning -Message "Failed to optimize output buffer size from $($Actual.Width)x$($Actual.Height) to $($Expected.Width)x$($Expected.Height)";
        } else {
            Write-Warning -Message "Failed to optimize output buffer width from $($Actual.Width) to $($Expected.Width)";
        }
    } else {
        if ($Expected.Height -ne $CurrentHost.UI.RawUI.BufferSize.Height) {
            Write-Warning -Message "Failed to optimize output buffer height from $($Actual.Height) to $($Expected.Height)";
        }
    }
    if ($ConsoleOptimize -ne 'None') {
        if ($null -eq $CurrentHost.UI.RawUI.MaxWindowSize) {
            Write-Warning -Message "Current console host `"$($CurrentHost.Name)`" does not allow modifying console size";
        } else {
            $Expected = $Actual;
            $Original = $CurrentHost.UI.RawUI.WindowSize;
            if ($ConsoleOptimize -eq 'MaxWidth') { $Expected = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $Actual.Width, $Original.Height }
            if ($Expected.Width -gt $CurrentHost.UI.RawUI.MaxWindowSize.Width) {
                if ($Expected.Height -gt $CurrentHost.UI.RawUI.MaxWindowSize.Height) {
                    $Expected = $CurrentHost.UI.RawUI.WindowSize = $CurrentHost.UI.RawUI.MaxWindowSize;
                } else {
                    $Expected = $CurrentHost.UI.RawUI.WindowSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $CurrentHost.UI.RawUI.MaxWindowSize.Width, $Expected.Height;
                }
            } else {
                if ($Expected.Height -gt $CurrentHost.UI.RawUI.MaxWindowSize.Height) {
                    $Expected = $CurrentHost.UI.RawUI.WindowSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $Expected.Width, $CurrentHost.UI.RawUI.MaxWindowSize.Height;
                }
            }
            if ($Expected.Width -ne $CurrentHost.UI.RawUI.WindowSize.Width) {
                if ($Expected.Height -ne $CurrentHost.UI.RawUI.WindowSize.Height) {
                    Write-Warning -Message "Failed to optimize console size from $($Original.Width)x$($Original.Height) to $($Expected.Width)x$($Expected.Height)";
                } else {
                    Write-Warning -Message "Failed to optimize console width from $($Original.Width) to $($Expected.Width)";
                }
            } else {
                if ($Expected.Height -ne $CurrentHost.UI.RawUI.WindowSize.Height) {
                    Write-Warning -Message "Failed to optimize console height from $($Original.Height) to $($Expected.Height)";
                }
            }
        }
    }
}

Push-Location -LiteralPath (Get-Location);
try {
    Set-Location -LiteralPath $PSScriptRoot;
    Optimize-PsHostBufferSize -MinOutBufferWidth $MinOutBufferWidth -MinOutBufferHeight $MinOutBufferHeight -ConsoleOptimize $ConsoleOptimize;

    $XmlDocument = New-Object -TypeName 'System.Xml.XmlDocument';
    $Script:SolutionFilePath = $PSScriptRoot | Join-Path -ChildPath $SolutionFile;
    $Script:SolutionDirectory = $Script:SolutionFilePath | Split-Path -Parent;
    $MSBuildExePath = $MsBuildBinPath | Join-Path -ChildPath 'MSBuild.exe';
    $BuildDateTime = [DateTime]::Now;
    $OutputFile = 'Build';
    $ArgumentList = "/t:$($Target -join ';')", "/verbosity:Detailed", "/p:Configuration=`"$Configuration`"", "/p:Platform=`"$Platform`"";
    $InputFiles = @();
    if ($null -ne $Project -and @($Project).Count -gt 0) {
        if (@($Project).Count -eq 1) { $OutputFile = "$([System.Xml.XmlConvert]::EncodeLocalName($Project))_" }
        $InputFiles = @(($Project | ForEach-Object {
            ((($PSScriptRoot | Join-Path -ChildPath 'src') | Join-Path -ChildPath $_) | Get-ChildItem -Filter "$_.*proj") | Select-Object -First 1;
        } | Select-Object -ExpandProperty 'FullName') | ForEach-Object {
            (($_ | Split-Path -Parent) | Split-Path -Leaf) | Join-Path -ChildPath ($_ | Split-Path -Leaf);
        });
    } else {
        $InputFiles = @('SnPowerShell.sln');
    }
    $OutputFile = "$OutputFile`Result_$($BuildDateTime.ToString('yyyyMMddHHmm')).xml"
    $ArgumentList += (@("/logger:XmlMsBuildLogger,PsMsBuildHelper\bin\PsMsBuildHelper.dll;$OutputFile") + @($InputFiles | ForEach-Object { 'src' | Join-Path -ChildPath $_ }));
    Write-Information -MessageData @"
Executing:
    $MSBuildExePath $(($ArgumentList | ForEach-Object { if ((-not $_.Contains('"')) -and $_ -match '\s') { "`"$_`"" } else { $_ } }) -join ' ')
"@ -InformationAction Continue;
return;
    $Process = Start-Process -FilePath $MSBuildExePath -WorkingDirectory $PSScriptRoot -ArgumentList $ArgumentList -PassThru -NoNewWindow;
    $StartTime = [DateTime]::Now;
    $TerminateScript = $ProcessTerminated = $false;
    try {
        $LastResponsive = [DateTime]::Now;
        while (-not $Process.HasExited) {
            [TimeSpan]$Elapsed = [DateTime]::Now.Subtract($Process.StartTime);
            $ElapsedMsg = '';
            if ($Elapsed.TotalMinutes -lt 1.0) {
                if ($Elapsed.Seconds -eq 1) { $ElapsedMsg = '1 second' } else { $ElapsedMsg = "$($Elapsed.Seconds) seconds" }
            } else {
                if ($Elapsed.TotalMinutes -lt 2.0) {
                    if ($Elapsed.Seconds -eq 1) { $ElapsedMsg = '1 minute' } else { $ElapsedMsg = "1 minute, $($Elapsed.Seconds) seconds" }
                } else {
                    if ($Elapsed.Seconds -eq 1) {
                        $ElapsedMsg = "$([Math]::Floor($Elapsed.TotalMinutes)) minutes, 1 second";
                    } else {
                        $ElapsedMsg = "$([Math]::Floor($Elapsed.TotalMinutes)) minutes, $($Elapsed.Seconds) seconds";
                    }
                }
            }
            if ($Process.Responding) {
                $LastResponsive = [DateTime]::Now;
                Write-Progress -Activity 'Build' -Status "CPU: $($Process.CPU); PM: $($Process.CPU); NPM: $($Process.CPU)" -CurrentOperation "$ElapsedMsg elapsed";
            } else {
                $Elapsed = [DateTime]::Now.Subtract($LastResponsive);
                if ($Elapsed.TotalSeconds -gt 15.0 -and -not $Process.HasExited) {
                    Write-Progress -Activity 'Build' -Status "Unresponsive $Elapsed" -Completed;
                    [System.Collections.ObjectModel.Collection[System.Management.Automation.Host.ChoiceDescription]]$ChoiceCollection = `
                        (New-Object -TypeName 'System.Management.Automation.Host.ChoiceDescription' -ArgumentList 'Abort', 'Abort build process'),
                        (New-Object -TypeName 'System.Management.Automation.Host.ChoiceDescription' -ArgumentList 'Wait', 'Wait for build process to become responsive'),
                        (New-Object -TypeName 'System.Management.Automation.Host.ChoiceDescription' -ArgumentList 'Exit', 'Exit script');
                        $UnresponsiveMsg = '';
                        if ($Elapsed.TotalMinutes -lt 1.0) {
                            $UnresponsiveMsg = "$($Elapsed.Seconds) seconds";
                        } else {
                            if ($Elapsed.TotalMinutes -lt 2.0) {
                                if ($Elapsed.Seconds -eq 1) { $UnresponsiveMsg = '1 minute' } else { $UnresponsiveMsg = "1 minute, $($Elapsed.Seconds) seconds" }
                            } else {
                                if ($Elapsed.Seconds -eq 1) {
                                    $UnresponsiveMsg = "$([Math]::Floor($Elapsed.TotalMinutes)) minutes, 1 second";
                                } else {
                                    $UnresponsiveMsg = "$([Math]::Floor($Elapsed.TotalMinutes)) minutes, $($Elapsed.Seconds) seconds";
                                }
                            }
                        }
                        $TerminateScript = $true;
                    switch ($Host.UI.PromptForChoice('Process Unresponsive', @"
$($Process.Name) (PID=$($Process.Id)) has been unresponsive for $UnresponsiveMsg.
What action would you like to take?
"@, $ChoiceCollection, 0)) {
                        0 {
                            $ProcessTerminated = $true;
                            Write-Progress -Activity 'Build' -Status 'Terminating' -CurrentOperation 'Waiting for process to stop';
                            $Process | Stop-Process -Force;
                            break;
                        }
                        1 { $TerminateScript = $false; break; }
                    }
                    if ($TerminateScript) { break }
                    Write-Progress -Activity 'Build' -Status "Unresponsive $UnresponsiveMsg" -CurrentOperation "$ElapsedMsg elapsed";
                } else {
                    Write-Progress -Activity 'Build' -Status "Unresponsive $UnresponsiveMsg" -CurrentOperation "$ElapsedMsg elapsed";
                }
            }
            Start-Sleep -Seconds 1;
        }
        $ExitTime = [DateTime]::Now;
        [TimeSpan]$Elapsed = $ExitTime.Subtract($StartTime);
        if ($Process.HasExited) {
            $Process | Wait-Process;
            if ($null -ne $Process.ExitTime) { $ExitTime = $Process.ExitTime }
            if ($null -ne $Process.StartTime) { $StartTime = $Process.StartTime }
            [TimeSpan]$Elapsed = $ExitTime.Subtract($StartTime);
            if ($ProcessTerminated) {
                Write-Progress -Activity 'Build' -Status 'Terminated' -Completed;
                Write-Warning -Message 'Build process aborted';
            } else {
                if ($Process.ExitCode -eq 0) {
                    Write-Progress -Activity 'Build' -Status 'Success' -Completed;
                } else {
                    Write-Progress -Activity 'Build' -Status "Fail Code $($Process.ExitCode)" -Completed;
                    Write-Warning -Message "Build process returned exit code $($Process.ExitCode)";
                }
                Write-Information -MessageData @"
Start Time: $($StartTime)
    Exit Time: $($ExitTime)
    Duration: $Elapsed
    Peak Paged Memory Size: $($Process.PeakPagedMemorySize64)
Peak Virtual Memory Size: $($Process.PeakVirtualMemorySize64)
"@ -InformationAction Continue;
            }
        } else {
            if ($ProcessTerminated) {
                $TerminateBy = [DateTime]::Now.AddSeconds(14);
                while ([DateTime]::Now -lt $TerminateBy -and -not $Process.HasExited) {
                    Write-Progress -Activity 'Build' -Status 'Terminating' -CurrentOperation 'Waiting for process to stop' -SecondsRemaining ($TerminateBy.Subtract([DateTime]::Now).Seconds + 1);
                    Start-Sleep -Seconds 1;
                }
                $Process | Wait-Process -Timeout 1;
                if ($Process.HasExited) {
                    Write-Progress -Activity 'Build' -Status 'Terminated' -Completed;
                } else {
                    Write-Progress -Activity 'Build' -Status 'Exited' -Completed;
                    Write-Warning -Message "Kill signal sent to build process (PID=$($Process.Id)) but has not yet terminated.";
                }
            } else {
                Write-Progress -Activity 'Build' -Status 'Exited' -Completed;
                Write-Warning -Message "Script is terminating, but the build process (PID=$($Process.Id)) but has not yet completed.";
            }
        }
    } finally {
        $OutputFile = $PSScriptRoot | Join-Path -ChildPath $OutputFile;
        if ($OutputFile | Test-Path) {
            $XmlDocument = New-Object -TypeName 'System.Xml.XmlDocument';
            $XmlDocument.Load($OutputFile);
            if ($null -eq $XmlDocument.DocumentElement) {
                Write-Error -Message 'Unable to read from build output file' -Category InvalidData -TargetObject $OutputFile;
            } else {
                $MessageObjects = @(@($XmlDocument.DocumentElement.SelectNodes('Message|Error|Warning')) | ForEach-Object {
                    $Properties = @{ };
                    $XmlAttribute = $_.SelectSingleNode('@Timestamp');
                    if ($null -eq $XmlAttribute) {
                        $Properties['Timestamp'] = [DateTime]::Now;
                    } else {
                        try { $Properties['Timestamp'] = [System.Xml.XmlConvert]::ToDateTime($XmlAttribute.Value, 'yyyy-MM-ddTHH:mm:ss.fffffffzzzzzz') }
                        catch { $Properties['Timestamp'] = [DateTime]::Now }
                    }
                    $XmlAttribute = $_.SelectSingleNode('@Importance');
                    if ($null -eq $XmlAttribute -or $XmlAttribute.Value.Trim().Length -eq 0) {
                        if ($_.PSBase.LocalName -eq 'Error' -or $_.PSBase.LocalName -eq 'Warning') { $Properties['Level'] = $_.PSBase.LocalName } else { $Properties['Level'] = 'Low' }
                    } else {
                        $Properties['Level'] = $XmlAttribute.Value;
                    }
                    $XmlAttribute = $_.SelectSingleNode('@Line');
                    if ($null -eq $XmlAttribute -or $XmlAttribute.Value.Trim().Length -eq 0) {
                        $Properties['Line'] = $null;
                    } else {
                        try { $Properties['Line'] = [System.Xml.XmlConvert]::ToInt32($XmlAttribute.Value) }
                        catch { $Properties['Line'] = $null }
                    }
                    $XmlAttribute = $_.SelectSingleNode('@Column');
                    if ($null -eq $XmlAttribute -or $XmlAttribute.Value.Trim().Length -eq 0) {
                        $Properties['Column'] = [DBNull]::Value;
                    } else {
                        try { $Properties['Column'] = [System.Xml.XmlConvert]::ToInt32($XmlAttribute.Value) }
                        catch { $Properties['Column'] = [DBNull]::Value }
                    }
                    $XmlAttribute = $_.SelectSingleNode('@File');
                    if ($null -eq $XmlAttribute) { $Properties['File'] = '' } else { $Properties['File'] = $XmlAttribute.Value.Trim() }
                    $XmlAttribute = $_.SelectSingleNode('@ProjectFile');
                    if ($null -eq $XmlAttribute) { $Properties['Project'] = '' } else { $Properties['Project'] = $XmlAttribute.Value.Trim() }
                    $XmlAttribute = $_.SelectSingleNode('@Code');
                    if ($null -eq $XmlAttribute) { $Properties['Code'] = '' } else { $Properties['Code'] = $XmlAttribute.Value }
                    $XmlAttribute = $_.SelectSingleNode('@Subcategory');
                    if ($null -eq $XmlAttribute) { $Properties['Subcategory'] = '' } else { $Properties['Subcategory'] = $XmlAttribute.Value }
                    $e = $_.SelectSingleNode('Message');
                    if ($null -eq $e -or $e.IsEmpty) { $Properties['Message'] = '' } else { $Properties['Message'] = $e.InnerText }
                    ('File', 'Project') | ForEach-Object {
                        if (($Properties[$_].Length + 2) -gt $PSScriptRoot.Length -and $Properties[$_].Substring(0, $PSScriptRoot.Length) -ieq $PSScriptRoot) {
                            $Properties[$_] = $Properties[$_].Substring($PSScriptRoot.Length);
                            if ($Properties[$_].StartsWith("\")) { $Properties[$_] = $Properties[$_].Substring(1) }
                        }
                    }
                    New-Object -TypeName 'System.Management.Automation.PSObject' -Property $Properties;
                } | Sort-Object -Property 'Timestamp');
                $ErrorObjects = @($MessageObjects | Where-Object { $_.Level -eq 'Error' -or $_.Level -eq 'Warning' });
                if ($ErrorObjects.Count -gt 0) { $MessageObjects = $ErrorObjects }
                $GridViewTitle = 'Build completed';
                @($XmlDocument.DocumentElement.SelectNodes('ProjectFinished')) | ForEach-Object {
                    $XmlAttribute = $_.SelectSingleNode('@File');
                    if ($null -eq $XmlAttribute) {
                        Write-Warning -Message 'Could not find ProjectFinished/@File attribute';
                    } else {
                        Write-Information -MessageData "Project Finished: $($XmlAttribute.Value)";
                    }
                    $XmlAttribute = $_.SelectSingleNode('@Succeeded');
                    if ($null -eq $XmlAttribute) {
                        Write-Warning -Message 'Could not find ProjectFinished/@Succeeded attribute';
                    } else {
                        $Succeeded = [System.Xml.XmlConvert]::ToBoolean($XmlAttribute.Value);
                        $XmlElement = $_.SelectSingleNode('Message');
                        if ($Succeeded) {
                            if ($null -eq $XmlElement -or $XmlElement.IsEmpty -or $XmlElement.InnerText.Trim().Length -eq 0) {
                                'Build succeeded' | Write-Host;
                            } else {
                                $XmlElement.InnerText | Write-Output;
                            }
                        } else {
                            if ($null -eq $XmlElement -or $XmlElement.IsEmpty -or $XmlElement.InnerText.Trim().Length -eq 0) {
                                Write-Warning -Message 'Build failed';
                            } else {
                                Write-Warning -Message $XmlElement.InnerText;
                            }
                        }
                    }
                }
                $XmlElement = $XmlDocument.DocumentElement.SelectSingleNode('BuildFinished');
                if ($null -eq $XmlElement) {
                    Write-Warning -Message 'Could not find BuildFinished element';
                } else {
                    $XmlAttribute = $XmlElement.SelectSingleNode('@Succeeded');
                    if ($null -eq $XmlAttribute) {
                        Write-Warning -Message 'Could not find BuildFinished/@Succeeded attribute';
                    } else {
                        $Succeeded = [System.Xml.XmlConvert]::ToBoolean($XmlAttribute.Value);
                        $XmlElement = $XmlElement.SelectSingleNode('Message');
                        if ($Succeeded) {
                            $GridViewTitle = 'Build succeeded';
                            if ($null -eq $XmlElement -or $XmlElement.IsEmpty -or $XmlElement.InnerText.Trim().Length -eq 0) {
                                $GridViewTitle | Write-Host;
                            } else {
                                $XmlElement.InnerText | Write-Output;
                            }
                        } else {
                            $GridViewTitle = 'Build failed';
                            if ($null -eq $XmlElement -or $XmlElement.IsEmpty -or $XmlElement.InnerText.Trim().Length -eq 0) {
                                Write-Warning -Message $GridViewTitle;
                            } else {
                                Write-Warning -Message $XmlElement.InnerText;
                            }
                        }
                    }
                }
                $MessageObjects | Out-GridView -Title $GridViewTitle;
            }
            Write-Information -MessageData "Build results saved to $OutputFile" -InformationAction Continue;
        } else {
            if (-not $TerminateScript) {
                Write-Error -Message 'Build output file not found' -Category ObjectNotFound -TargetObject $OutputFile;
            }
        }
    }
} finally { Pop-Location }
