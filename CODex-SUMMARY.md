# Change Summary (2025-10-15 22:03:42)
## Changed files
```
A	.github/pull_request_template.md A	.github/workflows/codex-summaries.yml A	.gitignore A	.vscode/tasks.json A	CONTRIBUTING.md M	README.md A	ServiceBench.App/App.xaml A	ServiceBench.App/App.xaml.cs A	ServiceBench.App/Assets/chart.min.js A	ServiceBench.App/Assets/index-template.html A	ServiceBench.App/Assets/report-template.html A	ServiceBench.App/Assets/styles.css A	ServiceBench.App/Converters/CountToVisibilityConverter.cs A	ServiceBench.App/Models/AppSettings.cs A	ServiceBench.App/Models/Branding.cs A	ServiceBench.App/Models/Config.cs A	ServiceBench.App/Models/DeviceInfo.cs A	ServiceBench.App/Models/LiveTelemetry.cs A	ServiceBench.App/Models/RunJson.cs A	ServiceBench.App/Models/TestPlan.cs A	ServiceBench.App/ServiceBench.App.csproj A	ServiceBench.App/Services/AidaController.cs A	ServiceBench.App/Services/DeviceDetector.cs A	ServiceBench.App/Services/FileManager.cs A	ServiceBench.App/Services/FurMarkController.cs A	ServiceBench.App/Services/HotkeyService.cs A	ServiceBench.App/Services/HwTelemetryService.cs A	ServiceBench.App/Services/OcctController.cs A	ServiceBench.App/Services/ProcessRunner.cs A	ServiceBench.App/Services/ReportGenerator.cs A	ServiceBench.App/Services/ScreenshotService.cs A	ServiceBench.App/Services/SettingsService.cs A	ServiceBench.App/Services/TelemetryCollector.cs A	ServiceBench.App/Utilities/SlugHelper.cs A	ServiceBench.App/ViewModels/MainViewModel.cs A	ServiceBench.App/ViewModels/RelayCommand.cs A	ServiceBench.App/ViewModels/SettingsViewModel.cs A	ServiceBench.App/ViewModels/ViewModelBase.cs A	ServiceBench.App/Views/MainWindow.xaml A	ServiceBench.App/Views/MainWindow.xaml.cs A	ServiceBench.App/Views/SettingsWindow.xaml A	ServiceBench.App/Views/SettingsWindow.xaml.cs A	ServiceBench.App/app.manifest A	ServiceBench.sln A	Test/.gitkeep A	Test/AIDA64/.gitkeep A	Test/FurMark/.gitkeep A	tools/fix-inline-task.ps1 A	tools/update-codex-summary.ps1
```
## NuGet packages (dotnet list package)
```
Project 'ServiceBench.App' has the following package references
   [net8.0-windows10.0.19041]: 
   Top-level Package              Requested   Resolved
   > LibreHardwareMonitorLib      0.9.4       0.9.4   
   > System.Drawing.Common        8.0.0       8.0.0   
   > System.Management            9.0.0       9.0.0   


```
## csproj diff
```
diff --git a/ServiceBench.App/ServiceBench.App.csproj b/ServiceBench.App/ServiceBench.App.csproj new file mode 100644 index 0000000..e3d18d8 --- /dev/null +++ b/ServiceBench.App/ServiceBench.App.csproj @@ -0,0 +1,26 @@ +<Project Sdk="Microsoft.NET.Sdk"> +  <PropertyGroup> +    <OutputType>WinExe</OutputType> +    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework> +    <Nullable>enable</Nullable> +    <UseWPF>true</UseWPF> +    <Platforms>x64</Platforms> +    <RuntimeIdentifier>win-x64</RuntimeIdentifier> +    <ApplicationManifest>app.manifest</ApplicationManifest> +  </PropertyGroup> +  <ItemGroup> +    <PackageReference Include="System.Management" Version="9.0.0" /> +    <PackageReference Include="System.Drawing.Common" Version="8.0.0" /> +    <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.4" /> +  </ItemGroup> +  <ItemGroup> +    <None Update="Assets\**\*"> +      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory> +    </None> +    <Resource Include="Assets\**\*" /> +  </ItemGroup> + +  <Target Name="EnsureObjWritable" BeforeTargets="ResolveReferences" Condition="'$(OS)' == 'Windows_NT'"> +    <Exec Command="attrib -R &quot;$(BaseIntermediateOutputPath)*&quot; /S /D" IgnoreExitCode="true" /> +  </Target> +</Project>
```
## sln diff
```
diff --git a/ServiceBench.sln b/ServiceBench.sln new file mode 100644 index 0000000..4d95a51 --- /dev/null +++ b/ServiceBench.sln @@ -0,0 +1,21 @@ +Microsoft Visual Studio Solution File, Format Version 12.00 +# Visual Studio Version 17 +VisualStudioVersion = 17.0.31903.59 +MinimumVisualStudioVersion = 10.0.40219.1 +Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ServiceBench.App", "ServiceBench.App\ServiceBench.App.csproj", "{E53CFDCE-2B09-4D28-B915-6DFB671F5B20}" +EndProject +Global +    GlobalSection(SolutionConfigurationPlatforms) = preSolution +        Debug|Any CPU = Debug|Any CPU +        Release|Any CPU = Release|Any CPU +    EndGlobalSection +    GlobalSection(ProjectConfigurationPlatforms) = postSolution +        {E53CFDCE-2B09-4D28-B915-6DFB671F5B20}.Debug|Any CPU.ActiveCfg = Debug|Any CPU +        {E53CFDCE-2B09-4D28-B915-6DFB671F5B20}.Debug|Any CPU.Build.0 = Debug|Any CPU +        {E53CFDCE-2B09-4D28-B915-6DFB671F5B20}.Release|Any CPU.ActiveCfg = Release|Any CPU +        {E53CFDCE-2B09-4D28-B915-6DFB671F5B20}.Release|Any CPU.Build.0 = Release|Any CPU +    EndGlobalSection +    GlobalSection(SolutionProperties) = preSolution +        HideSolutionNode = FALSE +    EndGlobalSection +EndGlobal
```
## XAML files changed
```
ServiceBench.App/App.xaml
ServiceBench.App/Views/MainWindow.xaml
ServiceBench.App/Views/SettingsWindow.xaml
```
## Build (Release)
- Status: **OK**
```
  Determining projects to restore...
  All projects are up-to-date for restore.
D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_ldnlpanz_wpftmp.csproj]
D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
  ServiceBench.App -> D:\a\testpc\testpc\ServiceBench.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\ServiceBench.App.dll

Build succeeded.

D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_ldnlpanz_wpftmp.csproj]
D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
    2 Warning(s)
    0 Error(s)

Time Elapsed 00:00:14.42

Workload updates are available. Run `dotnet workload list` for more information.

```

