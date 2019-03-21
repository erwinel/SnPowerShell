# SnPowerShell #

Utility PowerShell scripts for use with ServiceNow administration

## Build Instructions ##

### Requirements ###

* .NET Framework SDK Developer Pack (version 4.6+): <https://www.microsoft.com/net/download/visual-studio-sdks>

* Microsoft Build Tools: <https://www.microsoft.com/en-us/download/details.aspx?id=48159>

* Windows Managememt Framework (version 5+ _- aka. PowerShell_): <https://www.microsoft.com/en-us/download/details.aspx?id=54616>

### Build Script Parameters ###

Depending upon your system configuration, you may need to modify script parameter default values and/or options. Following describes the parameters and their purpose:

* __Configuration__: This specifies the build configuration as defined in the solution and used in project files, such as `Debug` and `Release`.
This parameter needs to either be manditory or to have a default value. The `ValidateSet` attribute should contain all configuration names that are defined in
*"src/SnPowerShell.sln"*.

* __Platform__: Defines the build platform as defined in the solution file and used in project files. This should default to 'Any CPU'. The `ValidateSet` attribute should contain all platform names that are defined in *"src/SnPowerShell.sln"*.

* __Target__: Specifies the MS Build targets. This parameter needs to either be manditory or to have a default value.

* __Project__: Specifies individual project names to build. If this is omitted, then all projects in *"src/SnPowerShell.sln"* will be built.
The `ValidateScript` attribute checks to see if there is a sub-folder matching the value of this parameter inside the *"src"* folder,
and that this matching folder contains a project file with the same base name and has an extension ending in *"proj"*.
For instance, if *"HtmlUtils"* is passed to this parameter, then it will check to see if a file matching *"src\\HtmlUtils\\HtmlUtils.\*proj"* exists,
relative to the `Build.ps1` script *(such as "src\\HtmlUtils\\HtmlUtils.csproj")*.

* __MsBuildBinPath__: Tells the build script where the MSBuild binaries can be found.

  * If you installed the Microsoft Build Tools manually, this will probably be *"`[Program Files]`\\MSBuild\\__Version__\\Bin"*
    (ex. *"C:\\Program Files (x86)\\MSBuild\\14.0\\Bin"*).

  * If Microsoft Visual Studio is installed, you may be able to find it in a path similar to
    *"`[Program Files]`\\Microsoft Visual Studio\\__Year__\\__LicenceType__\\MSBuild\\__Version__\\Bin"*
    (ex. *"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Professional\\MSBuild\\15.0\\Bin"*)