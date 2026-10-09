---
name: accessibility-review-perform
description: "Review exact commits or an exact diff through the accessibility lens: find the changed UI surfaces, check them against WCAG 2.2 AA criteria and platform guidelines with automated checkers plus manual reasoning, and report findings and an approve or rework disposition. Use when a change adds or alters a user interface on web, mobile, desktop, or game clients."
category: review
volatility: evolving
reviewed: 2026-10-09
---

# Perform Accessibility Review

## Use When
- A change adds or alters a user interface: markup, components, styles, themes, navigation, dialogs, forms, media, or UI copy.
- The plan or the orchestrator assigns the accessibility gate, or the user asks for an accessibility review.

## Do Not Use When
- No user-facing surface changed → `code-review-perform` only.
- Implementing the fixes → the owner of the changed UI.

## Inputs
- The pull request or commit range carrying the UI change, and its base.
- The intent (issue, plan, spec, or UX specification) and the target platforms.
- `commands.run`, `commands.e2e`, and `commands.lint` from the profile, when set.

## Steps
1. Run the review under «Review Protocol» in [review.md](../../wiki/workflows/review.md), against exact commit IDs from `git log --oneline <base>..<head>`.
2. List each changed UI surface from the diff: screens, components, and their states (default, hover, focus, disabled, loading, empty, error), plus every input method affected (pointer, touch, keyboard, switch, screen reader, voice).
3. Run the automated accessibility checks the project already has, for example a lint rule set in `commands.lint` or accessibility assertions in `commands.e2e`. Quote their output per «Evidence Format» in [verification.md](../../wiki/workflows/verification.md).
4. Walk «Checklist» below for each surface by reading the code. When `commands.run` is set, also exercise the running UI with the keyboard and the platform screen reader. Automated checkers detect only a minority of issues, so never approve on their results alone.
5. Map each finding to the WCAG 2.2 success criterion or platform guideline it violates, and rate it per «Severity» in review.md.
6. Report per «Output Format» and decide per «Dispositions» in review.md, citing the criterion in each finding.

## Checklist
- Semantics and roles: native elements or correct roles and traits; headings, landmarks, lists, and tables reflect the structure; custom controls expose name, role, value, and state (1.3.1, 4.1.2).
- Labels: every control, image, and icon-only button has an accessible name that contains its visible label; decorative images are hidden from assistive technology; form errors are described in text and tied to their field (1.1.1, 2.5.3, 3.3.1, 3.3.2).
- Keyboard and focus: every action works by keyboard with no trap; focus order follows reading order; focus is visible and not obscured; dialogs move focus in and return it on close (2.1.1, 2.1.2, 2.4.3, 2.4.7, 2.4.11).
- Announcements: status messages, errors, and loading changes are announced through live regions or platform announcement APIs without moving focus (4.1.3).
- Contrast and color: text at least 4.5:1, large text and UI component boundaries at least 3:1; color is never the only carrier of meaning (1.4.1, 1.4.3, 1.4.11).
- Motion: no content flashes more than three times per second; moving or auto-updating content can be paused; animation honors the platform reduced-motion setting (2.2.2, 2.3.1).
- Text scaling and reflow: content survives 200% text size and platform dynamic type without clipping or overlap, and reflows at 320 CSS px width (1.4.4, 1.4.10, 1.4.12).
- Touch targets: pointer targets at least 24×24 CSS px, and the platform minimum on mobile (44×44 pt iOS, 48×48 dp Android); dragging has a single-pointer alternative (2.5.7, 2.5.8).

## Output
- The report per «Output Format»: surfaces reviewed, checkers run, findings with their criterion, the disposition, and the reviewed commit IDs.
