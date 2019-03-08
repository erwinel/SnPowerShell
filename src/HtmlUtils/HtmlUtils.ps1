Import-Module -Name ($PSScriptRoot | Join-Path -ChildPath 'Module\Debug\Erwine.Leonard.T.SnPowerShell.HtmlUtils.dll') -ErrorAction Stop;

$DataSet = New-Object -TypeName 'HtmlUtils.UriParserDataSet';
if ([string]::IsNullOrEmpty($DataSet.CurrentSchemaValue)) {
    Write-Warning -Message "CurrentSchemaValue is empty";
} else {
    if ($DataSet.CurrentSchemaValue -ne [Uri]::UriSchemeHttp) {
        Write-Warning -Message "Expected CurrentSchemaValue: `"$([Uri]::UriSchemeHttp)`"; Actual: `"$($DataSet.CurrentSchemaValue)`"";
    }
}
if ([string]::IsNullOrEmpty($DataSet.SelectedSchemaValue)) {
    Write-Warning -Message "SelectedSchemaValue is empty";
} else {
    if ($DataSet.SelectedSchemaValue -ne [Uri]::UriSchemeHttp) {
        Write-Warning -Message "Expected SelectedSchemaValue: `"$([Uri]::UriSchemeHttp)`"; Actual: `"$($DataSet.SelectedSchemaValue)`"";
    }
}
if ($null -eq $DataSet.SelectedSchemaId) {
    Write-Warning -Message "SelectedSchemaId is empty";
} else {
    if ($DataSet.SelectedSchemaId -ne ([long]0)) {
        Write-Warning -Message "Expected SelectedSchemaId: 0; Actual: $($DataSet.SelectedSchemaId)";
    }
}
if ($DataSet.IsRelative) {
    Write-Warning -Message 'Expected IsRelative: false; Actual: true';
}
if (-not $DataSet.SchemeIsValid) {
    Write-Warning -Message 'Expected SchemeIsValid: true; Actual: false';
}
if (-not [string]::IsNullOrEmpty($DataSet.SchemeErrorMessage)) {
    Write-Warning -Message "Expected SchemeErrorMessage: (null or empty); Actual: `"$($DataSet.SchemeErrorMessage)`"";
}
<#
$DataSet.SelectedSchemaId = 1;
if ([string]::IsNullOrEmpty($DataSet.CurrentSchemaValue)) {
    Write-Warning -Message "CurrentSchemaValue is empty";
} else {
    if ($DataSet.CurrentSchemaValue -ne [Uri]::UriSchemeHttps) {
        Write-Warning -Message "Expected CurrentSchemaValue: `"$([Uri]::UriSchemeHttps)`"; Actual: `"$($DataSet.CurrentSchemaValue)`"";
    }
}
if ([string]::IsNullOrEmpty($DataSet.SelectedSchemaValue)) {
    Write-Warning -Message "SelectedSchemaValue is empty";
} else {
    if ($DataSet.SelectedSchemaValue -ne [Uri]::UriSchemeHttps) {
        Write-Warning -Message "Expected SelectedSchemaValue: `"$([Uri]::UriSchemeHttps)`"; Actual: `"$($DataSet.SelectedSchemaValue)`"";
    }
}
if ($null -eq $DataSet.SelectedSchemaId) {
    Write-Warning -Message "SelectedSchemaId is empty";
} else {
    if ($DataSet.SelectedSchemaId -ne ([long]1)) {
        Write-Warning -Message "Expected SelectedSchemaId: 1; Actual: $($DataSet.SelectedSchemaId)";
    }
}
if ($DataSet.IsRelative) {
    Write-Warning -Message 'Expected IsRelative: false; Actual: true';
}
if (-not $DataSet.SchemeIsValid) {
    Write-Warning -Message 'Expected SchemeIsValid: true; Actual: false';
}
if (-not [string]::IsNullOrEmpty($DataSet.SchemeErrorMessage)) {
    Write-Warning -Message "Expected SchemeErrorMessage: (null or empty); Actual: `"$($DataSet.SchemeErrorMessage)`"";
}

#>