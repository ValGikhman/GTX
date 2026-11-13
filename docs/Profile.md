# Company Profile

Open **Majordome → Profile** (`/Profile`) to edit the shared company details.
Only the built-in Majordome administrator can see or edit Profile. Owner and every other
regular role are excluded, even if an old database permission still includes Profile.

## Initial fields

- Company name: GTX Autogroup
- Email: gtxagroup@gmail.com
- Phone: (513) 489-2886
- Address: 9516 Princeton Glendale Rd, West Chester, OH 45011
- Research radius: 100 miles (allowed range: 1–500)
- Documentary fee: $398 (whole USD, allowed range: 0–10,000)
- Business hours: Monday–Friday 9am–7pm, Saturday 9am–6pm, Sunday 11am–4pm
- Optional address line 2; stored map coordinates are shown as read-only values

The address ZIP is also the default research ZIP. There is no separate ZIP setting.
Appraisal (individual, batch and embedded), chatbot research, contact links, directions,
mobile configuration and inventory export identity now read the profile.
The documentary fee used by vehicle prices, comparisons, chatbot results and APIs is also read from Profile.
Business hours support minute-level opening/closing times or a closed day. Closing must be later on the same day.
Schedule displays and open/closed indicators read the saved hours without the old hour-long cache.
Explicit ZIP/radius values on a research request still override the defaults.
Company identity in translated marketing copy, legal text, heroes, contact widgets and
chat prompts is resolved from the same profile. Infrastructure URLs and credentials remain separate.

## Production setup

1. Back up the intended production database through the normal process.
2. Run `Scripts/Sql/CreateSiteProfile.sql` against that database **before deploying this application version**.
3. Deploy the application, then open Majordome → Profile and verify the contact details.
4. Sign in with the built-in administrator and verify Profile is hidden from regular roles.

The script creates `dbo.SiteProfile`, with a single row (`Id=1`), and a seven-row
`dbo.SiteProfileBusinessHours` table. It also upgrades an earlier Profile table with the fee column.
Rerunning it does not overwrite edited profile values, hours or role permissions.
The application reads and updates the row; it does not create tables at runtime and has no
hardcoded fallback values. A missing table or row must be fixed by running the setup script.
Map coordinates remain available for inventory exports, but are not editable or submitted
by the Profile form. Profile saves preserve their existing database values.

Company details, fee and all seven business-hours rows save in one transaction.
Each HTTP request reuses its profile read; later requests see committed changes without an application restart.
The legacy XML opening-hours entries are no longer read at runtime.

## Validation

After a Debug build, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/SiteProfile.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/SiteRoles.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/SiteProfile.Database.Tests.ps1
```

The database test uses and removes an isolated temporary database on local SQL Server only.
Useful future additions: website/social links and holiday-hour exceptions.
SMTP/API credentials and infrastructure connection settings should stay outside this profile.
