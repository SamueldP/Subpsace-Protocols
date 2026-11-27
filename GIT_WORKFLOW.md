# Git & Release Workflow (Antigravity Instructions)

## Global Safety Rules
- NEVER run `git push --force`, `git reset --hard`, or delete user files.
- NEVER push directly to `main` during normal backups.
- NEVER commit build artifacts or `.zip` archives. Ensure `.gitignore` ignores `bin/`, `obj/`, `release/`, `.vs/`, and `*.zip`.
- If a merge conflict, authentication error, or file-size warning occurs, STOP immediately and explain the problem in plain English.

---

## 1. Everyday Backup (Default)
Trigger: "Run an Everyday Backup"
1. Verify `.gitignore` excludes `bin/`, `obj/`, `release/`, `.vs/`, and `*.zip`. Add missing entries if needed.
2. Check the active Git branch. If `dev` does not exist yet, create it from the current state; otherwise switch to `dev` without losing uncommitted changes.
3. Review `git status`, stage all valid project files, and commit with a short, clear summary of what changed.
4. Push to GitHub: `git push -u origin dev`.
5. Reply with the branch name, commit message, and short commit hash.

---

## 2. Milestone Push to Main
Trigger: "Run a Milestone Push to Main"
1. Confirm `dev` has no uncommitted changes (commit them to `dev` first if any exist).
2. Switch to `main`, merge `dev` into `main`, and push `main` to `origin`.
3. Create an annotated Git tag using today's date and a short label (e.g., `backup-2026-11-27-milestone`) and push the tag to `origin`.
4. Immediately switch back to `dev` so future work never lands on `main`.
5. Reply with the tag name and confirm the repo is back on `dev`.

---

## 3. Publish Playtest Release
Trigger: "Publish a Playtest Release"
1. Check that GitHub CLI is installed and logged in (`gh auth status`). If not, STOP and tell the user.
2. Verify `release\SubspaceProtocols_win-x64` exists and has the latest Release build.
3. Zip `release\SubspaceProtocols_win-x64` via PowerShell (`Compress-Archive`) into a cleanly named file like `SubspaceProtocols_win-x64_v0.x-playtest.zip` (do NOT commit the zip to Git).
4. Run `gh release create` on the current branch with `--prerelease`, attach the `.zip`, and include brief bullet-point release notes.
5. Reply with the direct GitHub Release URL.

---

## 4. Emergency Undo
Trigger: "Run an Emergency Undo"
1. Do NOT delete or overwrite anything yet.
2. Run `git status` and `git log -n 5 --oneline` and explain in plain English what changed since the last clean backup.
3. Wait for confirmation before restoring files or reverting to a previous commit.
