# AGENTS.md

## Project stack
- ASP.NET MVC 5 / .NET Framework 4.8
- Razor views
- jQuery
- Bootstrap 5
- SQL Server

## Rules
- Do not migrate this project to ASP.NET Core unless explicitly asked.
- Prefer small, safe patches.
- Before changing routes, check RouteConfig, WebApiConfig, Global.asax, controller attributes, and IIS behavior.
- For JavaScript, keep compatibility with existing jQuery patterns.
- Show diffs before major changes.
- Do not modify production config files unless explicitly asked.