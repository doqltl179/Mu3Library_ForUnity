---
owns: "The watchlist of external tools, standards, and model families the kit depends on, with their sources, status, and last check date"
volatility: volatile
reviewed: 2026-10-09
sources: ["https://code.claude.com/docs/en/overview", "https://developers.openai.com/codex", "https://docs.github.com/en/copilot", "https://agents.md", "https://agentskills.io"]
---

# Tech Radar

The register `trend-scout` maintains: what the kit watches, where to look, and when it was last checked. Stack packs are not listed here; each pack's `sources` and freshness cadence cover it.

## When

- running a radar scan,
- adding a dependency of the kit on an external tool, format, or standard.

## Route Away When

- the scan procedure: the `kit-trend-scan` skill,
- recording a change found during a scan: [change-intake.md](change-intake.md).

## Statuses

| Status | Meaning |
|---|---|
| `supported` | The kit renders files for it or relies on it; changes can break projects |
| `tracking` | Watched because it may become supported or change a practice |
| `deprecated` | Support is being removed; projects have migration notes |

## Watchlist

| Item | Kind | Status | Sources | Kit files affected | Last checked | Check every (days) |
|---|---|---|---|---|---|---|
| Claude Code | AI tool | `supported` | [docs](https://code.claude.com/docs/en/overview), [changelog](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md) | `tools/agentkit.py` adapters, [tool-adapters.md](../integration/tool-adapters.md) | 2026-10-09 | 30 |
| OpenAI Codex | AI tool | `supported` | [docs](https://developers.openai.com/codex), [releases](https://github.com/openai/codex/releases) | Same as above | 2026-10-09 | 30 |
| GitHub Copilot | AI tool | `supported` | [docs](https://docs.github.com/en/copilot), [changelog](https://github.blog/changelog/) | Same as above | 2026-10-09 | 30 |
| Cursor | AI tool | `supported` | [docs](https://cursor.com/docs), [changelog](https://cursor.com/changelog) | Same as above | 2026-10-09 | 60 |
| Gemini CLI | AI tool | `supported` | [repository](https://github.com/google-gemini/gemini-cli) | Same as above | 2026-10-09 | 60 |
| AGENTS.md convention | Standard | `supported` | [agents.md](https://agents.md) | [START.md](../../START.md) rendering | 2026-10-09 | 90 |
| Agent Skills format | Standard | `supported` | [agentskills.io](https://agentskills.io) | [skill-spec.md](../authoring/skill-spec.md) | 2026-10-09 | 90 |
| Model Context Protocol | Standard | `tracking` | [modelcontextprotocol.io](https://modelcontextprotocol.io) | None yet | 2026-10-09 | 90 |
| Frontier model families | Models | `tracking` | Vendor model pages and release notes | [model-calibration.md](model-calibration.md), `[models.*]` in profiles | 2026-10-09 | 30 |
| Conventional Commits, SemVer, Keep a Changelog | Standard | `supported` | [conventionalcommits.org](https://www.conventionalcommits.org), [semver.org](https://semver.org), [keepachangelog.com](https://keepachangelog.com) | [git-workflow.md](../workflows/git-workflow.md), [release.md](../workflows/release.md) | 2026-10-09 | 365 |
| WCAG | Standard | `supported` | [WCAG 2.2](https://www.w3.org/TR/WCAG22/) | `accessibility-review-perform` skill | 2026-10-09 | 180 |

## How To Update

- After each scan, set «Last checked» for every item scanned, even when nothing changed.
- Add an item when the kit starts depending on something new; remove one only through a `deprecated` period.
- Change a status only through an accepted intake record.
- Set this page's `reviewed` date when the whole watchlist has been checked.
