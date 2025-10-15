# Change Summary (2025-10-15 21:20:27)
## Changed files
```
M	ServiceBench.App/ServiceBench.App.csproj
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
diff --git a/ServiceBench.App/ServiceBench.App.csproj b/ServiceBench.App/ServiceBench.App.csproj index a367280..e3d18d8 100644 --- a/ServiceBench.App/ServiceBench.App.csproj +++ b/ServiceBench.App/ServiceBench.App.csproj @@ -12,8 +12,6 @@      <PackageReference Include="System.Management" Version="9.0.0" />      <PackageReference Include="System.Drawing.Common" Version="8.0.0" />      <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.4" /> -    <PackageReference Include="UIAutomationClient" Version="8.0.0" /> -    <PackageReference Include="UIAutomationTypes" Version="8.0.0" />    </ItemGroup>    <ItemGroup>      <None Update="Assets\**\*">
```
## sln diff
```

```
## Build (Release)
- Status: **OK**
```
  Determining projects to restore...
  All projects are up-to-date for restore.
D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_ean3kdti_wpftmp.csproj]
D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
  ServiceBench.App -> D:\a\testpc\testpc\ServiceBench.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\ServiceBench.App.dll

Build succeeded.

D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App_ean3kdti_wpftmp.csproj]
D:\a\testpc\testpc\ServiceBench.App\ViewModels\MainViewModel.cs(825,25): warning CS8602: Dereference of a possibly null reference. [D:\a\testpc\testpc\ServiceBench.App\ServiceBench.App.csproj]
    2 Warning(s)
    0 Error(s)

Time Elapsed 00:00:15.39

Workload updates are available. Run `dotnet workload list` for more information.

```

