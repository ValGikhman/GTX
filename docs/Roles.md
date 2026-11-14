# Roles and the built-in administrator

Majordome is the built-in administrator. Its identity is fixed in code; its password
is configured separately. It is never stored in the editable role table, listed in
Roles, or available to edit, rename, delete, or impersonate through that page.

Only Majordome can see or access Roles, Profile and Services. Owner is an ordinary editable,
renameable and deletable role. All regular roles start with access to the other
Majordome pages, and their individual page permissions remain editable. Existing
roles receive these initial grants once, using `dbo.SiteRoleSetup` marker `Id=2`;
later application restarts preserve permission edits. Old Roles/Profile/Services grants
never authorize regular roles.

The existing administrator-only CDN purge actions require Majordome. Mobile login
uses its own fixed password from the private `App_Data/MobileManagement.config`
file; website role and administrator password changes do not affect it. Provision
that file separately on each host and preserve it across publishes. It stores only
a salted PBKDF2 hash in `<mobileManagement passwordHash="..." />`, never the plain
password. A missing or invalid file disables mobile login. Ordinary staff pages,
chat appraisal, and VIN decoding use the current role's menu permissions.

## Set or change the password

After building the application, run this in Windows PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/Set-MajordomePassword.ps1
```

The script asks for the password twice and saves only a salted PBKDF2 hash to
`App_Data/Majordome.config`. The password is never written to source code or printed.
The file is ignored by Git and must be provisioned separately on each server;
the application does not include a fallback password. To target another host's
application folder, pass `-Path 'C:\path\to\site\App_Data\Majordome.config'`.
Keep this file in the protected App_Data directory, outside public static content.

Hosting can instead supply the same hash in the process environment variable
`GTX_MAJORDOME_PASSWORD_HASH`. That variable takes precedence over the private file.
A missing or invalid hash disables administrator login; regular database roles
continue to work. Changing the hash invalidates existing administrator web
sessions. A process environment change may require restarting the host.

The password-only login checks Majordome first. If an existing regular role has
the same password, set a different password for that role from Roles; its record
and existing password are not silently changed during this upgrade.

## Validation

Build, then run the role regression scripts in Windows PowerShell. Database tests
create and remove their own temporary database on local SQL Server and never use
the configured application database.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/SiteRoles.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/MajordomeAccount.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/SiteRoles.Database.Tests.ps1
node tests/SiteRoleNavigation.Tests.js
```
