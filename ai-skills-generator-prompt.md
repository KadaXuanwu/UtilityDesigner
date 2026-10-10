# Generate AI Skills for a Unity Asset Store package

You are an expert Unity Editor engineer and technical writer. Your job is to author production-quality **AI Skills** for the Unity Asset Store package described in the configuration below, plus a test suite and maintenance hooks.

These skills will ship inside the package to paying customers, and will be catalogued alongside skills from many other publishers so that Unity AI and other agents can discover and drive this asset. **The output format is a shared standard — follow it exactly.** Your creativity belongs in the content (accurate, useful, agent-ready instructions), not in the structure.

---

## Step 0 — Configuration

The publisher fills in **one required line**. The other two are optional overrides.

```
ASSET_STORE_URL:  <https://assetstore.unity.com/packages/...>                (required)
DOCS_URL:         <documentation link — leave blank to auto-discover>        (optional)
ASSET_FOLDER:     <e.g. Assets/ExampleStudios/TerraForgePro — leave blank to auto-detect>  (optional)
```

Everything else — asset name, publisher, version, category, supported Unity versions, render pipelines, support link — **you resolve yourself** in Phase 1 and present for confirmation in Phase 2. In the templates below, `{curly-brace}` values are for you to fill in with real, resolved content.

### How to resolve the configuration

- **From the store page** — fetch `ASSET_STORE_URL` if you have web access. It provides the asset name, publisher name, latest version, supported Unity versions, render-pipeline compatibility, category, and usually documentation/support links.
- **From the URL string itself** — works even without web access: the path encodes the category and asset slug, e.g. `assetstore.unity.com/packages/tools/terrain/terraforge-pro-12345` → category `tools/terrain`, asset "TerraForge Pro".
- **From the project** — if `ASSET_FOLDER` is blank, locate the asset by matching the asset/publisher names against `Assets/` (publisher projects are almost always `Assets/<Publisher>/<Asset>` or `Assets/<Asset>`; a CHANGELOG, version file, or editor scripts matching the asset name confirm it). Corroborate the version from files inside the folder, and pipeline support from `Packages/manifest.json` and pipeline-specific code paths.
- **DOCS_URL** — if blank, use links found on the store page or README/manual files inside the asset folder.
- **If something can't be resolved** (no web access, multiple candidate folders, conflicting versions): flag it in your Phase 2 plan and ask. Never guess silently.

---

## Ground rules

1. **Truth comes from source.** Every menu path, class, method, namespace, default value, and workflow you write must be verified in the actual code and docs under `ASSET_FOLDER` (and `DOCS_URL` if you have web access). Never invent an API. If you cannot verify something, leave it out — an agent following a wrong instruction is worse than one following no instruction.
2. **Touch nothing you don't own.** The only thing you create under `Assets/` is `ASSET_FOLDER/AI/Skills/`. Never modify the asset's source code, scenes, prefabs, or settings. Test files live outside `Assets/` entirely.
3. **Skills ship to customers.** Professional, plain English. No marketing copy, no pricing, no internal notes, no secrets, no TODO/FIXME markers.
4. **Your reader is an AI agent operating the Unity Editor** (with a human developer reading over its shoulder). Write in the imperative. Give exact menu paths in `Window > Menu > Item` form, runnable C# with full namespaces, and the expected observable result after each step. Where a rule exists for a reason, state the reason — agents follow instructions better when they understand why.
5. **The format is a standard.** Do not rename, reorder, add, or remove the required frontmatter fields or the required section headings. Consistency across publishers is what makes store-wide cataloging possible.
6. **Stop and ask** before writing anything if this doesn't appear to be a Unity project, you cannot confidently identify the asset's folder, or resolved details conflict with what you see in the project.
7. Markdown files only, UTF-8.

---

## What you will create

```
<ProjectRoot>/
├── ASSET_FOLDER/
│   └── AI/
│       └── Skills/                        ← ships with the package
│           ├── {prefix}-setup-and-overview/
│           │   └── SKILL.md
│           ├── {prefix}-{capability}/
│           │   ├── SKILL.md
│           │   └── references/            ← optional, for overflow detail
│           └── ...
├── AISkillTests/                          ← project root, NEVER inside Assets/
│   ├── README.md
│   ├── RESULTS.md
│   └── scenarios/
│       └── {skill-name}/
│           ├── S001-{slug}.md
│           └── S002-{slug}.md
├── CLAUDE.md                              ← maintenance section added/appended
└── AGENTS.md                              ← same section, added/appended
```

Why this layout: skills live inside `ASSET_FOLDER` so they are exported with the `.unitypackage` and reach every customer. Tests live at the project root so they stay in the publisher's repo and can never accidentally ship.

---

## Deliverable 1 — AI Skills

### How many, and which

- **3 to 8 skills total.**
- **Skill #1 is mandatory:** `{prefix}-setup-and-overview` — what the asset is, how to verify it's installed, first-time project setup (pipeline configuration, scripting defines, sample import), where key files live, and a one-line index of the other skills.
- The remaining skills each cover **one job a user would ask for in a single sentence** ("generate a terrain", "spawn vegetation", "bake the navmesh"). Not one skill per API method; not one mega-skill for the whole manual. Prioritize the workflows customers most often want, judged from the asset's docs, samples, and editor UI.

### Naming

- `{prefix}` = the resolved asset name, lowercased, kebab-case (e.g. `TerraForge Pro` → `terraforge-pro`).
- Folder name and frontmatter `name` are identical: `{prefix}-{capability}`.
- Lowercase letters, digits, and hyphens only (`^[a-z0-9-]+$`), 64 characters max.

### SKILL.md frontmatter — exact schema

```yaml
---
name: {prefix}-{capability}
description: "{see description rules below}"
metadata:
  asset: "{resolved asset name}"
  publisher: "{resolved publisher name}"
  asset-version: "{resolved asset version}"
  skill-version: "1.0.0"
  unity: "{minimum supported Unity version}+"
  render-pipelines: "{supported pipelines, e.g. Built-in, URP, HDRP}"
  category: "{store category path, lowercase, e.g. tools/terrain}"
  asset-store-url: "{ASSET_STORE_URL}"
  documentation-url: "{resolved docs URL}"
  support-url: "{resolved support URL}"
  last-verified: "{today's date, YYYY-MM-DD}"
---
```

All values are quoted strings. `category` is the Asset Store's own category path taken from the listing/URL — do not invent your own taxonomy. Omit `documentation-url` / `support-url` if none exists. Add nothing else to the frontmatter.

### Description rules

The description is the **single most important thing you will write** — it is how a catalog and an agent decide whether this skill is relevant, usually without reading anything else. Requirements:

- Third person, one string, **1024 characters max**.
- Starts with **"Use this skill whenever"**.
- Packs in the concrete phrases a real user would type, quoted, in their voice — casual, imperative, imperfect: `'make me a terrain'`, `'add mountains to my scene'`, `'why is my spawner not working'`. Cover synonyms and phrasings that don't name the asset.
- States what the skill covers **and** what it does not, with a pointer: `Do NOT use for vegetation spawning (see {prefix}-spawn-biomes).`
- Errs pushy: agents under-trigger skills more than they over-trigger. Close with something like: `When in doubt whether a {domain} request could involve {ASSET_NAME}, use this skill — Prerequisites shows how to confirm the asset is installed.`

**Example (fictional asset):**

```
"Use this skill whenever the user wants to create, generate, or sculpt terrain with TerraForge Pro — e.g. 'make me a terrain', 'generate an island', 'add mountains to the north side', 'stamp this heightmap onto my terrain'. Covers creating new terrains, applying stamps, and importing heightmaps in the Unity Editor. Do NOT use for vegetation or object spawning (see terraforge-pro-spawn-biomes) or for runtime procedural generation. When in doubt whether a terrain request could involve TerraForge Pro, use this skill — the Prerequisites section shows how to confirm the asset is installed."
```

### SKILL.md body — exact heading skeleton

Every skill uses these headings, with this exact text, in this exact order:

```markdown
# {Human-readable title, e.g. Create Terrain with TerraForge Pro}

{One-paragraph summary: what this skill lets an agent accomplish, in plain language.}

## When to use this skill
## Prerequisites
## Quick start
## Workflows
### Workflow: {name}        ← repeat one H3 per workflow
## Verification
## API quick reference
## Common issues
## Boundaries
```

What goes in each section:

- **When to use this skill** — bulleted concrete triggers (example user requests), then a short "Not for:" list pointing to sibling skills or out-of-scope territory.
- **Prerequisites** — required asset version, Unity version, render pipeline, dependencies, and — critically — a **programmatic install check** an agent can run before proceeding (e.g. "confirm the type `{Namespace.TypeName}` exists" or "confirm the menu `{Window > X > Y}` is present" or "confirm the folder `{path}` exists"). State what to tell the user if the check fails (including the Asset Store link).
- **Quick start** — the single most common task, end to end, as numbered steps. Exact menu paths and/or a runnable C# editor snippet. End with the expected observable result ("the Hierarchy now contains…").
- **Workflows** — each `### Workflow:` has **Goal**, **Steps**, **Expected result**. Prefer actions an agent can actually perform: menu paths, Editor API calls with full namespaces in fenced `csharp` blocks, asset/file operations. Name important parameters, their defaults, and when to change them.
- **Verification** — machine-checkable success signals: objects/components that should exist in the scene, property values, files on disk, a clean Console. Written so an agent can confirm its own work without a human.
- **API quick reference** — a table (`Entry point | Type | What it does`) of only the handful of classes/methods an agent actually needs. Push anything deeper into `references/` files, linked with a one-line note on when to read them.
- **Common issues** — Symptom → Cause → Fix, for the failures users actually hit (mine the docs, FAQ, and code comments).
- **Boundaries** — what the asset genuinely cannot do, when to fall back to native Unity or another tool, and any feature gated to a different edition/tier.

### Size and progressive disclosure

Keep each SKILL.md **under 500 lines**. If a skill needs more, move detail into `references/{topic}.md` inside the skill folder and link it from the relevant section with guidance on when to read it. The SKILL.md itself must remain sufficient for the common cases without opening any reference.

---

## Deliverable 2 — Skill tests (outside Assets/)

Create `AISkillTests/` as a **sibling of `Assets/`** at the project root. It must never be placed under `Assets/` — it is repo-only and must not ship in the package.

### Scenarios

At least **2 scenarios per skill**: one `basic`, one `intermediate` or `advanced`. File path: `AISkillTests/scenarios/{skill-name}/S{NNN}-{short-slug}.md`.

Scenario prompts must read like real user messages — concrete, casual, sometimes lowercase, sometimes missing the asset's name entirely. Not "Create a terrain using the CreateTerrain API" but "ok i need a terrain for my open world game, like 1km square, mountains up north".

**Exact scenario format:**

````markdown
---
id: {skill-name}-S{NNN}
skill: {skill-name}
title: "{what this scenario proves, in one line}"
difficulty: basic        # basic | intermediate | advanced
---

## Setup
- {Unity version / project state required}
- {e.g. Open a new empty scene (File > New Scene > Basic)}
- {any assets, files, or scene objects that must exist first}

## Prompt
Give the agent exactly this message, nothing else:

> {realistic user message}

## Expected behavior
- {which skill the agent should invoke}
- {which workflow it should follow, roughly}
- {it should verify its own result using the skill's Verification section}

## Pass criteria
- [ ] {machine-checkable outcome, e.g. scene contains X with property Y ≈ Z}
- [ ] {…}
- [ ] No compile errors or unhandled exceptions in the Console
- [ ] Completed without human intervention

Optional scripted check (run in the Editor, e.g. as a temporary Editor script):

```csharp
{short snippet that logs PASS or FAIL — only if the outcome can be checked in code}
```

## Scoring
- **Pass** — all criteria met, no human intervention
- **Partial** — outcome achieved but a criterion missed, or one human assist needed
- **Fail** — wrong or no outcome, errors, or the agent gave up
````

Make pass criteria objectively checkable wherever possible; save visual judgment ("mountains look natural") for at most one criterion per scenario.

### README.md — the benchmark protocol

Write `AISkillTests/README.md` explaining how to run and benchmark, including this two-mode protocol:

1. **Baseline run** — fresh scene / reverted project state; give the agent the scenario's Prompt with the skills **unavailable** (temporarily rename `AI/Skills` to `AI/Skills~`, or use an agent session without access to them). Record the result.
2. **Skill run** — identical starting state, skills available. Record the result.
3. Log both rows in `RESULTS.md`. Reset project state (revert via version control, new scene) between every run.

The baseline-vs-skill delta — pass rate, wall-clock time, tokens/credits — is the benchmark, and the publisher's evidence that skills make agents better with their asset.

### RESULTS.md — the log

Create it with this header and one example row marked as an example:

```markdown
| Date | Agent / model | Scenario | Mode | Result | Time | Tokens/credits | Notes |
|------|---------------|----------|------|--------|------|----------------|-------|
```

`Mode` is `baseline` or `skill`. `Result` is `Pass`, `Partial`, or `Fail`.

---

## Deliverable 3 — Maintenance hooks

Create `CLAUDE.md` and `AGENTS.md` at the project root if they don't exist; if they exist, append. Insert this exact section (with values resolved) into **both** files:

```markdown
## AI skill maintenance ({ASSET_NAME})

This project ships AI Skills at `{ASSET_FOLDER}/AI/Skills/` and skill tests at `AISkillTests/`. They are living documents — keep them true:

1. **When code changes:** any change to a public API, menu item, editor window, default value, or workflow that a skill documents must update that SKILL.md in the same change set. Bump its `skill-version` and set `last-verified` to today.
2. **When you (the agent) use {ASSET_NAME} and a skill instruction turns out wrong, unclear, or missing:** fix the skill immediately, then add a note to `AISkillTests/RESULTS.md` describing what was corrected.
3. **When a feature ships:** decide whether it needs a new skill or a new workflow in an existing skill, and add at least one matching test scenario under `AISkillTests/scenarios/`.
4. **Before every Asset Store submission:** re-run all scenarios in skill mode, log results in `RESULTS.md`, and fix failures first.
5. Never delete a skill without replacing its coverage or marking it deprecated in its `description`.
6. Never change the required frontmatter fields or section headings — the structure is a shared Asset Store standard used for cataloging.
```

---

## Working process

**Phase 1 — Resolve and explore (read before you write).** Resolve the configuration per Step 0: fetch the store page if you have web access, parse the URL, locate the asset's folder under `Assets/`, and determine name, publisher, version, category, supported Unity versions, and pipelines. Then inventory the asset: `[MenuItem]` attributes, `EditorWindow` classes, public managers and entry-point APIs, `ScriptableObject` types, sample scenes, README/CHANGELOG/manuals inside the folder. Read the key documentation pages if reachable. Note anything pipeline- or version-specific.

**Phase 2 — Plan (approval gate).** Output two things: (1) the **resolved configuration** as a short table — asset, publisher, folder, version, Unity versions, pipelines, category, docs/support links — flagging anything you could not resolve; (2) the **proposed skill set** as a table: `name | human title | one-line scope | key workflows covered`, plus a short list of what you're deliberately *not* covering and why. Then **stop** and wait for the publisher to reply "proceed" or with corrections. This pause always happens — it is where the auto-detected values get human sign-off.

**Phase 3 — Author the skills.** Verify every claim against source as you write. When you write a menu path, you have seen the `[MenuItem]` string; when you write an API call, you have seen the signature.

**Phase 4 — Author the tests.** Scenarios, README, RESULTS as specified.

**Phase 5 — Maintenance hooks.** CLAUDE.md and AGENTS.md as specified.

**Phase 6 — Self-review, then report.** Check every item below, fix what fails, then output the final report.

---

## Self-review checklist

- [ ] Skill folder names equal their frontmatter `name`; all match `^[a-z0-9-]+$`, start with `{prefix}`, ≤64 chars
- [ ] Every description starts with "Use this skill whenever", is ≤1024 characters, quotes concrete trigger phrases, and includes at least one "Do NOT use for" pointer
- [ ] Frontmatter has exactly the required fields; `last-verified` is today's date
- [ ] All required body headings present, exact text, exact order, in every SKILL.md
- [ ] `{prefix}-setup-and-overview` exists and indexes the other skills
- [ ] Every menu path, type, method, and default was verified in the asset's source or docs
- [ ] Each SKILL.md ≤500 lines; overflow moved to `references/`
- [ ] No marketing language, pricing, internal notes, or TODO markers in any shipped file
- [ ] ≥2 scenarios per skill; every scenario has all required sections; prompts read like real user messages; pass criteria are mostly machine-checkable
- [ ] Nothing created under `Assets/` except `{ASSET_FOLDER}/AI/Skills/`; nothing test-related under `Assets/`; no asset source modified
- [ ] `CLAUDE.md` and `AGENTS.md` both contain the maintenance section

---

## Final report (output in chat when done)

1. **File tree** of everything created.
2. **Skills table** — each `name` with the first sentence of its description.
3. **Verification evidence** — for each skill, one or two examples of "claim → source file where confirmed".
4. **Coverage gaps** — workflows intentionally left out, and anything you could not verify and therefore omitted.
5. **Next steps for the publisher:**
   - Review every skill for accuracy — you own correctness.
   - Focus the Unity Editor once so `.meta` files generate for `AI/Skills` before exporting the package.
   - Run each skill's S001 scenario in baseline mode and skill mode; log both in `AISkillTests/RESULTS.md`.
   - Commit `AISkillTests/` to source control; exclude it from the package export.
   - Include `AI/Skills` in the next package submission, and register the skills for cataloging per the Asset Store instructions.
