# AGENTS.md

## Project stack
- ASP.NET MVC 5 / .NET Framework 4.8
- Razor views
- jQuery
- Bootstrap 5
- SQL Server
- Linq to SQL

## Rules
- Do not migrate this project to ASP.NET Core unless explicitly asked.
- Prefer small, safe patches.
- Use plain ASCII punctuation in assistant replies and new or edited user-facing project text, including labels, sample prompts, chatbot replies, and documentation. Never use em dash (U+2014) or en dash (U+2013) characters; use commas, periods, semicolons, or the ASCII hyphen (-) instead.
- Before changing routes, check RouteConfig, WebApiConfig, Global.asax, controller attributes, and IIS behavior.
- For JavaScript, keep compatibility with existing jQuery patterns.
- Show diffs before major changes.
- Do not modify production config files unless explicitly asked.
- Write GTX website chatbot replies as compact rhyming verses, normally four lines with no heading or blank lines, while keeping technical details clear and accurate, use Jewish flavour humor. This applies only to the chatbot implemented in the application. Preserve exact facts, source code, commands, paths, and logs.
- Always use existing Bootstrap style over the creating new, unless there no bootstrap styles defined for this element.

## Deploy commands
When the user issues `deploy` as a command without a destination (case-insensitive, including `please deploy`), deploy to sandbox. `deploy to sandbox` selects the same workflow. `deploy to production` selects production. Mentioning or discussing a command does not trigger it.

1. Inspect the working tree and fetch origin. Review pending task changes and the commits that would enter the selected release. For production, review all Development commits that would enter master.
2. Fix issues found in the review, clean up unused code, and make small, evidence-based performance improvements within the task scope. Preserve active behavior and unrelated user changes. Show diffs before major changes.
3. Run the application build, relevant regression tests, and `git diff --check`. Fix regressions introduced by the changes. Report unrelated existing failures explicitly; do not describe failed checks as passing.
4. Commit the reviewed task changes to `Development` and push `origin/Development`. Use a different release source for sandbox if the user specifies one. Do not include unrelated metadata, secrets, generated output, or production configuration changes without explicit authorization. Use the existing release commit when no new commit is needed.
5. For an explicit production deploy, update local `master` from `origin/master`, merge `Development` into `master`, and push `origin/master`. Resolve routine merge conflicts and revalidate affected behavior. Use an isolated worktree when necessary to preserve local work. Never force-push.
6. Verify the remote release commit and leave the main workspace on `Development`. Use the `gtx-website-publish` skill to publish and verify the pushed release for the selected environment. Sandbox uses the pushed Development release by default. Production uses the pushed `master` release.
7. Report the commit and any merge hash, validation results, website publishing status, and any remaining local changes. Report a live update only after verifying it. If the hosting connection is missing, report which Git and release preparation steps completed.

The command authorizes the commit, push, and website publish for the selected environment. Production changes require an explicit production destination. Stop only for a genuine blocker, an unresolved regression, or an ambiguity that cannot safely be resolved from context.
