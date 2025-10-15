# Change Summary (2025-10-15 20:47:35)
## Changed files
```
M	ServiceBench.App/Assets/index-template.html M	ServiceBench.App/Assets/report-template.html M	ServiceBench.App/Assets/styles.css M	ServiceBench.App/Models/RunJson.cs M	ServiceBench.App/ServiceBench.App.csproj M	ServiceBench.App/Services/DeviceDetector.cs M	ServiceBench.App/Services/FurMarkController.cs M	ServiceBench.App/Services/OcctController.cs M	ServiceBench.App/Services/ReportGenerator.cs M	ServiceBench.App/Services/ScreenshotService.cs M	ServiceBench.App/ViewModels/MainViewModel.cs
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
diff --git a/ServiceBench.App/ServiceBench.App.csproj b/ServiceBench.App/ServiceBench.App.csproj index e3d18d8..0879d8e 100644 --- a/ServiceBench.App/ServiceBench.App.csproj +++ b/ServiceBench.App/ServiceBench.App.csproj @@ -13,6 +13,10 @@      <PackageReference Include="System.Drawing.Common" Version="8.0.0" />      <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.4" />    </ItemGroup> +  <ItemGroup> +    <Reference Include="UIAutomationClient" /> +    <Reference Include="UIAutomationTypes" /> +  </ItemGroup>    <ItemGroup>      <None Update="Assets\**\*">        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
```
## sln diff
```

```
## Build (Release)
- Status: **OK**
```
  Determining projects to restore...
  All projects are up-to-date for restore.
C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3245: Could not resolve this reference. Could not locate the assembly "UIAutomationClient". Check to make sure the assembly exists on disk. If this reference is required by your code, you may get compilation errors. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3245: Could not resolve this reference. Could not locate the assembly "UIAutomationTypes". Check to make sure the assembly exists on disk. If this reference is required by your code, you may get compilation errors. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3243: No way to resolve conflict between "UIAutomationClient, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" and "UIAutomationClient". Choosing "UIAutomationClient, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" arbitrarily. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3243: No way to resolve conflict between "UIAutomationTypes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" and "UIAutomationTypes". Choosing "UIAutomationTypes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" arbitrarily. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
D:\a\testpc\testpc\ServiceBench.App\Services\ReportGenerator.cs(178,42): error CS0246: The type or namespace name 'IReadOnlyCollection<>' could not be found (are you missing a using directive or an assembly reference?) [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_icsvcfxt_wpftmp.csproj]
D:\a\testpc\testpc\ServiceBench.App\Services\ReportGenerator.cs(198,44): error CS0246: The type or namespace name 'IReadOnlyCollection<>' could not be found (are you missing a using directive or an assembly reference?) [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_icsvcfxt_wpftmp.csproj]

Build FAILED.

C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3245: Could not resolve this reference. Could not locate the assembly "UIAutomationClient". Check to make sure the assembly exists on disk. If this reference is required by your code, you may get compilation errors. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3245: Could not resolve this reference. Could not locate the assembly "UIAutomationTypes". Check to make sure the assembly exists on disk. If this reference is required by your code, you may get compilation errors. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3243: No way to resolve conflict between "UIAutomationClient, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" and "UIAutomationClient". Choosing "UIAutomationClient, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" arbitrarily. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
C:\Program Files\dotnet\sdk\9.0.305\Microsoft.Common.CurrentVersion.targets(2433,5): warning MSB3243: No way to resolve conflict between "UIAutomationTypes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" and "UIAutomationTypes". Choosing "UIAutomationTypes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" arbitrarily. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
D:\a\testpc\testpc\ServiceBench.App\Services\ReportGenerator.cs(178,42): error CS0246: The type or namespace name 'IReadOnlyCollection<>' could not be found (are you missing a using directive or an assembly reference?) [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_icsvcfxt_wpftmp.csproj]
D:\a\testpc\testpc\ServiceBench.App\Services\ReportGenerator.cs(198,44): error CS0246: The type or namespace name 'IReadOnlyCollection<>' could not be found (are you missing a using directive or an assembly reference?) [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_icsvcfxt_wpftmp.csproj]
    4 Warning(s)
    2 Error(s)

Time Elapsed 00:00:13.36

Workload updates are available. Run `dotnet workload list` for more information.

```

