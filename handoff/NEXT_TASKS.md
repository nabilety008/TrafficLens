# NEXT TASKS — TrafficLens

Ordered follow-up work for future sessions, starting from the final v0.1.4 local
state. Each item states why it is deferred, what "done" means, and what to check.

**v0.1.4 verification and release documentation are already complete.** Nothing in
`handoff/CURRENT_STATE.md` or `docs/PACKAGING.md` needs redoing. Do not rebuild,
re-sign, re-package, or re-hash the v0.1.4 artifacts as a way of "making progress" —
those hashes are the record of record, and the release script overwrites them.

The dependency chain matters: 1 → 2 → 3 → 4 is strict, and 4 depends on 5.

---

## 0. Refresh `AGENTS.md` — independent, do this first — PENDING USER APPROVAL

**Why:** `AGENTS.md` is the file every agent is instructed to read first, and it is
stale: it still calls the app a WPF application, omits both WinUI projects from the
layout, and its build command omits the mandatory `-p:Platform=x64`. Following it
literally points an agent at the wrong UI project and at a command that cannot
succeed. The full drift table is in `handoff/KNOWN_ISSUES.md`.

**Why it is not done:** the v0.1.4 closeout was scoped to release documentation
only, and rewriting the repo's agent-instruction file was not part of that scope.
It needs the user's go-ahead.

**Do (with approval):**
- Correct the project framing to WinUI 3, keeping the accurate points: `TrafficLens.App`
  still exists and its `.resx` files and services are linked into the WinUI project.
- Add `TrafficLens.WinUI` and `TrafficLens.WinUI.Tray` to the layout, and list the
  test projects plus the `TrafficLens.Network.Verification` console harness.
- Fix the Verification section to `dotnet build TrafficLens.sln -c Release -p:Platform=x64`
  and note the `dotnet`/SDK-location requirement.

**Check:** an agent reading only `AGENTS.md` names the correct entry-point project
and produces a build command that runs.

**Careful:** this file is instruction, not a changelog. Keep it short and factual;
do not turn it into a second `NEXT_TASKS.md`.

---

## 1. Create the GitHub repository and add the remote — DEFERRED

**Why:** no remote exists. Every downstream step needs somewhere to push.

**Do:**
- Create the remote repository (private or public — the user has not decided).
- Decide the owner and repository name. **No owner, name, or URL is recorded in
  this repository; do not invent one.** Ask the user.
- `git remote add origin <url>` and verify with `git remote -v`.

**Check:** `git remote -v` lists `origin`; the local branch is unchanged.

**Careful:** adding a remote does not push. Nothing is published by this step.

---

## 2. Push the branch and tag `v0.1.4` — DEFERRED

**Why:** publication is the user's decision, not a build step.

**Do (in order):**
1. `git push -u origin feature/winui3-migration`
2. Confirm CI is green on the remote before continuing.
3. `git push origin 67c334e06aa0d567f50a75992bb042feec292a41` (pre-push history)
4. Tag the release commit as `v0.1.4` and push the tag.

Existing tags are `v0.1.0`, `v0.1.1`, `v0.1.2` — `v0.1.4` does not exist yet.
Confirm the tag name is still free before creating it.

**Check:** `git ls-remote --tags origin` lists `v0.1.4`; the commit it points at is
the release commit recorded in `handoff/CURRENT_STATE.md`.

**Careful:** push the tag only after the branch is on the remote. Do not merge to
`master` in the same step — see item 7.

---

## 3. Publish the GitHub Release with artifacts — DEFERRED

**Why:** needs items 1 and 2.

**Do:**
- Create the GitHub Release for `v0.1.4` from the `CHANGELOG.md` section.
- Upload the installer and the portable ZIP **plus their `.sha256` sidecars**.
- Copy the checksums from `handoff/CURRENT_STATE.md` verbatim.

Expected values (from `handoff/CURRENT_STATE.md`):

| Artifact | SHA-256 |
|---|---|
| `TrafficLens-Setup-0.1.4-win-x64.exe` | `A982E268E13AB03DD36BBEA335947CC886FF50170E255391D1D935F00E52D70E` |
| `TrafficLens-Portable-0.1.4-win-x64.zip` | `03A41E32F5A76837C8E58A1AC6D1EC557E4471A9D0F979669C511AC2BFA61D24` |

**Check:** re-download one asset and re-hash it locally; it must match.

**Careful:** the build is **unsigned** — say so in the release notes. Do not describe
the binaries as signed.

---

## 4. Implement the GitHub update checker — DEFERRED until item 3

**Why:** there is no owner, repository, or release URL. An updater pointed at a
guessed endpoint is worse than no updater. The
`IWindowsUpdateService` seam and `WindowsUpdateCommandBuilder` / `WindowsUpdateStateResolver`
already exist to make this a small change once a URL is real.

**Do:**
- Obtain the real release-feed URL from the user.
- Implement against the existing `IWindowsUpdateService` interface; do not
  re-architect settings or the tray around it.
- Keep the deferred, opt-in, "check for updates" behaviour — do **not** make the
  app auto-download or auto-install.
- Add tests for the state resolution and command building.

**Check:** tests for the new logic pass; manually verifying a check against a real
release reports "up to date".

**Careful:** do not hardcode a repository URL, and do not ship a default endpoint.

---

## 5. Clean-machine installer validation — DEFERRED

**Why:** install/upgrade/uninstall were never exercised, because an existing
installation owned the same AppId registration. The installer is verified only
statically.

**Do (in order, each on a clean machine or VM):**
1. **Fresh install** of `TrafficLens-Setup-0.1.4-win-x64.exe` into a machine with no
   prior TrafficLens. Verify install path, shortcut, and launch.
2. **Launch** on the clean machine and confirm the UI renders — this specifically
   re-proves the PRI fix on a machine that has never run this build.
3. **Upgrade** from a prior installed version, confirming settings and SQLite history
   survive and the superseded WPF `TrafficLens.exe` is removed by `[InstallDelete]`.
4. **Uninstall**, and record whether user data under `%LOCALAPPDATA%\TrafficLens\`
   is intentionally retained or removed.

**Check:** all four pass without a manual repair step.

**Careful:** do not run the silent installer on the development machine just to
tick a box — that is what collided with the existing registration. Uninstall the
old build first, or use a VM.

---

## 6. Extended stability run — DEFERRED

**Why:** the earlier WUI-009 audit was intentionally stopped before its long-duration
phase. There is no soak data.

**Do:** run the published build for an extended session and check, at the end:

- `%LOCALAPPDATA%\TrafficLens\logs\` — **zero** `"level":"Error"` records.
- CPU and memory flat, not climbing.
- ETW collector still healthy (or reporting the documented degraded
  `ProcessTrafficCollectorStatus` when unelevated).
- Widget drag/clamp still correct after a display change.
- History database growing sanely; pruning at the documented retention.

**Check:** no errors in the log, no leak, no degradation.

**Careful:** `TrafficLens.WinUI` must not already be running when you start a fresh
soak run; the single-instance guard will refuse the second launch.

---

## 7. Merge to `master` — DEFERRED, and it is *not* a conflict

**Why:** the user deferred merging. The technical analysis is done so a future
session does not repeat it.

**Already established:**
- `master` is a **direct ancestor** of `feature/winui3-migration`, so the merge is a
  fast-forward.
- No content conflict is possible in that situation.

**Do (only on explicit user instruction):**

```powershell
git checkout master
git merge --ff-only feature/winui3-migration
```

**Check:** `git status` is clean; `git log --oneline -1` is the release commit.

**Careful:** use `--ff-only`. If it is not a fast-forward after all, **stop** — that
means local history changed and the earlier analysis no longer holds.

---

## Not on the roadmap

- **No GUI redesign, new feature, or dependency addition** without asking. v0.1.4 is
  a completed, verified closeout; these are not gaps to be filled.
- **Do not create security policy files or VBS-enable blocks.** Nothing in this
  repository requires them, and the earlier `0x800711C7` "Smart App Control" reading
  was a misdiagnosis of the missing-PRI crash (see `handoff/KNOWN_ISSUES.md`).
