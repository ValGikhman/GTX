# GTX Car Care Center

Independent ASP.NET MVC 5 / .NET Framework 4.8 website for `carcare.usedcarscincinnati.com`.
The landing page is `/`; the services page is `/services`. No database, photo hosting,
third-party scripts, or production credentials are required. Calls use GTX's existing
number, (513) 489-2886. Service descriptions and contact details live in
`Models/CarCareContent.cs`.

## Run locally

1. Restore the solution's NuGet packages into the existing root `packages` directory.
2. Set `GTX.CarCare` as the startup project in Visual Studio and run with IIS Express,
   or build from the GTX directory:

   ```powershell
   .\build.ps1 -Solution GTX.CarCare\GTX.CarCare.csproj
   & 'C:\Program Files\IIS Express\iisexpress.exe' /path:"$PWD\GTX.CarCare" /port:51483 /systray:false
   ```

3. Open `http://localhost:51483/` and `http://localhost:51483/services`.

Use the full Visual Studio MSBuild with the ASP.NET web workload and .NET Framework
4.8 targeting pack. To additionally compile Razor views, build with
`/p:MvcBuildViews=true` using MSBuild.

## Shared styling

`Content/bootstrap/bootstrap.min.css`, all color and background palettes in `Content/Themes`, and
`Content/marketing-pages.css` in the parent GTX project remain the source of truth.
The new project's `CopySharedStyles` target copies them to its ignored
`Content/shared` folder before building. The existing Bootstrap dropdown script is
also copied to `Scripts/shared`. These files are included in folder publishing,
so the published site is self-contained and never requests assets from the dealership
website. Rebuild and republish Car Care after changing shared styles. Site-specific
layout and responsive rules live in `Content/carcare.css`. The selector matches
GTX's nine color choices, Light/Grey/Dark backgrounds, and `gtx-theme` /
`gtx-background-theme` localStorage and one-year cookie preferences. Preferences
are per origin, as on GTX; they are not synchronized across separate subdomains.
The matching menu/swatches are in `Content/theme-selector.css`.

## Publish later

Publish **GTX.CarCare**, not the whole solution, to a separate folder or IIS site.
For a local folder publish, run this from a Visual Studio Developer PowerShell in
the GTX directory (choose a new output folder):

```powershell
MSBuild.exe GTX.CarCare\GTX.CarCare.csproj /p:Configuration=Release /p:DeployOnBuild=true /p:DeployDefaultTarget=WebPublish /p:WebPublishMethod=FileSystem /p:DeleteExistingFiles=false /p:publishUrl=C:\Publish\GTX.CarCare
```

Use a .NET CLR v4.0 application pool in Integrated mode with ASP.NET 4.8 installed.
The site root must be the published Car Care folder, not a subdirectory application
under the live GTX website; it needs its own configuration and application pool.

Add the `carcare.usedcarscincinnati.com` host binding and an appropriate HTTPS
certificate on the hosting server. Point the hostname's DNS record at that server
through the domain's authoritative DNS provider. No DNS, live IIS bindings, or
existing GTX production configuration are changed by this project.

Confirm actual service availability before publishing. Prices, business hours,
and a Car Care street address are intentionally omitted until confirmed.
