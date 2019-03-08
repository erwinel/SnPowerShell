Param(
    [ValidateSet('Debug', 'Release')]
    # Build configuration.
    [string]$Configuration = 'Debug',

    # Target platform.
    [string]$Platform = 'AnyCPU',

    [ValidateSet('Test', 'Deploy', 'None')]
    # Action to take after successful build.
    [string]$Action = 'None',

    [ValidateSet('Build', 'Resources', 'Compile', 'Rebuild', 'Clean', 'Publish')]
    # Build targets.
    [string[]]$Targets = @('Build'),

    [ValidateSet('HtmlUtils')]
    # Individual project to build (assumes project file path is "src/$Project/$Project.csproj", relative to the location of this script). If omitted, all projects in src/SnPowerShell.sln will be built.
    [string]$Project,

    # Minimum output buffer width.
    [int]$TermMinWidth = 2048,

    # Minimum output buffer height.
    [int]$TermMinHeight = 6000,

    # Location of MSBuild binaries
    [ValidateScript({ $_ | Test-Path -PathType Container })]
    [string]$MsBuildBin = 'C:\Program Files (x86)\MSBuild\14.0\Bin'
)

Write-Information -MessageData "Verifying host configuration...";
Write-Information -MessageData "Host = `"$((Get-Host).Name)`"" -InformationAction Continue;

# Get host and system UI information to validate host output settings.
($HostRawUI, $MaxScreenSize) = &{
    $CurrentHost = Get-Host;
    if ($null -ne $CurrentHost -and $null -ne $CurrentHost.UI -and $null -ne $CurrentHost.UI.RawUI) {
        $CurrentHost.UI.RawUI | Write-Output;
        $Rectangle = (([System.Windows.Forms.Screen]::AllScreens | Select-Object -ExpandProperty 'WorkingArea') | Sort-Object -Property @{ Expression = { $_.Width * $_.Height } } -Descending) | Select-Object -First 1;
        if ($null -ne $MaxScreenSize -and -not $Rectangle.Size.IsEmpty) {
            (New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $Rectangle.Width, $Rectangle.Height) | Write-Output;
        } else {
            if ($null -ne $CurrentHost.UI.RawUI.MaxWindowSize -and -not ($CurrentHost.UI.RawUI.MaxWindowSize.Width -eq 0 -and $CurrentHost.UI.RawUI.MaxWindowSize.Height -eq 0)) {
                $CurrentHost.UI.RawUI.MaxWindowSize | Write-Output;
            } else {
                if ($null -ne $CurrentHost.UI.RawUI.BufferSize -and -not ($CurrentHost.UI.RawUI.BufferSize.Width -eq 0 -and $CurrentHost.UI.RawUI.BufferSize.Height -eq 0)) {
                    $CurrentHost.UI.RawUI.BufferSize | Write-Output;
                } else {
                    (New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList 800, 600) | Write-Output;
                }
            }
        }
    }
}
# Checking output buffer size (to give ample room for parsing build output messages).
if ($null -eq $HostRawUI) {
    Write-Information -MessageData @"
Failed to get host raw UI. Expected minimum $TermMinWidth`X$TermMinHeight output buffer size could not be verified.
This is only a cosmetic issue and does not affect any automated process.
"@ -InformationAction Continue;
} else {
    if ($null -eq $HostRawUI.BufferSize) {
        Write-Information -MessageData "Buffer size was null. Attempting to specify new buffer size" -InformationAction Continue;
        $HostRawUI.BufferSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $TermMinWidth, $TermMinHeight;
    } else {
        if ($HostRawUI.BufferSize.Height -lt $TermMinHeight) {
            if ($HostRawUI.BufferSize.Width -lt $TermMinWidth) {
                Write-Information -MessageData "Adjusting buffer size from $($HostRawUI.BufferSize.Width)X$($HostRawUI.BufferSize.Height) to $TermMinWidth`X$TermMinHeight" -InformationAction Continue;
                $HostRawUI.BufferSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $TermMinWidth, $TermMinHeight;
            } else {
                Write-Information -MessageData "Adjusting buffer height from $($HostRawUI.BufferSize.Height) to $TermMinHeight" -InformationAction Continue;
                $HostRawUI.BufferSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $HostRawUI.BufferSize.Width, $TermMinHeight;
            }
            $HostRawUI.BufferSize.Height = $TermMinHeight;
        } else {
            if ($HostRawUI.BufferSize.Width -lt $TermMinWidth) {
                Write-Information -MessageData "Adjusting buffer height from $($HostRawUI.BufferSize.Width) to $TermMinWidth" -InformationAction Continue;
                $HostRawUI.BufferSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $TermMinWidth, $HostRawUI.BufferSize.Height;
            }
        }
    }
    if ($null -eq $HostRawUI.BufferSize) {
        Write-Information -MessageData @"
Failed to set buffer size. Expected minimum $TermMinWidth`X$TermMinHeight output buffer size could not be verified.
This is only a cosmetic issue and does not affect any automated process.
"@ -InformationAction Continue;
    } else {
        if ($HostRawUI.BufferSize.Height -lt $TermMinHeight -or $HostRawUI.BufferSize.Width -lt $TermMinWidth) {
            Write-Information -MessageData @"
Failed to adjust buffer size. Expected minimum: $TermMinWidth`X$TermMinHeight; Actual: $($HostRawUI.BufferSize.Width)X$($HostRawUI.BufferSize.Height).
This is only a cosmetic issue and does not affect any automated process.
"@ -InformationAction Continue;
        } else {
            # Checking maximum output window size (for cosmetic purposes).
            if ($null -ne $HostRawUI.MaxWindowSize) {
                if ($HostRawUI.BufferSize.Height -lt $MaxScreenSize.Height) {
                    if ($HostRawUI.BufferSize.Width -lt $MaxScreenSize.Width) {
                        $MaxScreenSize = $HostRawUI.BufferSize;
                    } else {
                        $MaxScreenSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $MaxScreenSize.Width, $HostRawUI.BufferSize.Height;
                    }
                } else {
                    if ($HostRawUI.BufferSize.Width -lt $MaxScreenSize.Width) {
                        $MaxScreenSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $HostRawUI.BufferSize.Width, $MaxScreenSize.Height;
                    }
                }
                if ($HostRawUI.MaxWindowSize.Height -lt $MaxScreenSize.Height) {
                    if ($HostRawUI.MaxWindowSize.Width -lt $MaxScreenSize.Width) {
                        Write-Information -MessageData "Adjusting buffer size from $($HostRawUI.MaxWindowSize.Width)X$($HostRawUI.MaxWindowSize.Height) to $($MaxScreenSize.Width)`X$($MaxScreenSize.Height)" -InformationAction Continue;
                        $HostRawUI.MaxWindowSize.Height = $MaxScreenSize.Height;
                    } else {
                        Write-Information -MessageData "Adjusting buffer height from $($HostRawUI.MaxWindowSize.Height) to $($MaxScreenSize.Height)" -InformationAction Continue;
                        $HostRawUI.MaxWindowSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $HostRawUI.MaxWindowSize.Width, $MaxScreenSize.Height;
                    }
                } else {
                    if ($HostRawUI.MaxWindowSize.Width -lt $MaxScreenSize.Width) {
                        Write-Information -MessageData "Adjusting buffer height from $($HostRawUI.MaxWindowSize.Width) to $($MaxScreenSize.Width)" -InformationAction Continue;
                        $HostRawUI.MaxWindowSize = New-Object -TypeName 'System.Management.Automation.Host.Size' -ArgumentList $MaxScreenSize.Width, $HostRawUI.MaxWindowSize.Height;
                    }
                }
                if ($HostRawUI.MaxWindowSize.Height -lt $MaxScreenSize.Height -or $HostRawUI.MaxWindowSize.Width -lt $MaxScreenSize.Width) {
                    Write-Information -MessageData @"
Failed to adjust max window size. Expected maximum: $($MaxScreenSize.Width)`X$($MaxScreenSize.Height); Actual: $($HostRawUI.MaxWindowSize.Width)X$($HostRawUI.MaxWindowSize.Height).
This is only a cosmetic issue and does not affect any automated process.
"@ -InformationAction Continue;
                }
            }
        }
    }
}

$XmlDocument = New-Object -TypeName 'System.Xml.XmlDocument';
$Script:SolutionFilePath = $PSScriptRoot | Join-Path -ChildPath $SolutionFile;
$Script:SolutionDirectory = $Script:SolutionFilePath | Split-Path -Parent;
$MSBuildExePath = $MsBuildBin | Join-Path -ChildPath 'MSBuild.exe';
$OutputFile = 'BuildResult.xml';
$InputFile = 'src/SnPowerShell.sln';
if ($Project -ne $null -and $Project.Trim().Length -gt 0) {
    $OutputFile = "BuildResult_$Project.xml";
    $InputFile = "src/$Project/$Project.csproj";
}
$ArgumentList = "/t:$($Targets -join ';')", "/verbosity:Detailed", "/p:Configuration=`"$Configuration`"", "/p:Platform=`"$Platform`"",
    "/logger:XmlMsBuildLogger,PsMsBuildHelper\bin\PsMsBuildHelper.dll;$OutputFile", $InputFile;
Write-Information -MessageData @"
Executing:
    $MSBuildExePath $(($ArgumentList | ForEach-Object { if ((-not $_.Contains('"')) -and $_ -match '\s') { "`"$_`"" } else { $_ } }) -join ' ')
"@ -InformationAction Continue;
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
