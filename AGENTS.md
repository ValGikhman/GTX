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
- Write GTX website chatbot replies as compact rhyming verses, normally four lines with no heading or blank lines, while keeping technical details clear and accurate, use jewish humor. This applies only to the chatbot implemented in the application. Preserve exact facts, source code, commands, paths, and logs.

## Deploy command
When the user issues `deploy` as a command in this repository (case-insensitive, including `please deploy`), execute this workflow. Mentioning or discussing the command does not trigger it.

1. Inspect the working tree and fetch origin. Review pending task changes and all Development commits that would enter master.
2. Fix issues found in the review; clean up unused code and make small, evidence-based performance improvements within the task scope. Preserve active behavior and unrelated user changes. Show diffs before major changes.
3. Run the application build, relevant regression tests, and `git diff --check`. Fix regressions introduced by the changes. Report unrelated existing failures explicitly; do not describe failed checks as passing.
4. Commit the reviewed task changes to `Development` and push `origin/Development`. Do not include unrelated metadata, secrets, generated output, or production configuration changes without explicit authorization. If nothing needs committing, continue with any unmerged commits.
5. Update local `master` from `origin/master`, merge `Development` into `master`, and push `origin/master`. Use the actual branch name `master`, not `Master`. Resolve routine merge conflicts and revalidate affected behavior. Use an isolated worktree when necessary to preserve local work. Never force-push.
6. Verify the remote branch tips and leave the main workspace on `Development`. Report the commit and merge hashes, validation results, and any remaining local changes.

The `deploy` command authorizes the commit, pushes, and merge above without another confirmation. Stop only for a genuine blocker, an unresolved regression, or an ambiguity that cannot safely be resolved from context. This workflow ends with the Git merge and push; report website deployment status separately and only when verified.
