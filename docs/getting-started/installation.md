# Installation

## NuGet Package Manager

```shell
dotnet add package AccordionQ2.WebApiClient
```

Or via the Visual Studio Package Manager Console:

```powershell
Install-Package AccordionQ2.WebApiClient
```

Or by editing your `.csproj` directly:

```xml
<PackageReference Include="AccordionQ2.WebApiClient" Version="*" />
```

## Requirements

- **.NET Standard 2.0** compatible runtime:
  - .NET 5, 6, 7, 8, 9, 10+
  - .NET Framework 4.6.1+
  - Mono, Xamarin, Unity (with .NET Standard 2.0 support)
- An **AccordionQ2 WebApi** host reachable over HTTP (e.g. `http://raspberrypi:5000` or `http://agent64.local:5000`)

## Namespace

All public types are under:

```csharp
using AccordionQ2.WebApiClient;
using AccordionQ2.WebApiClient.Models;
```
