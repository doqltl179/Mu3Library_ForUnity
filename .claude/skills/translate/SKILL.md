---
name: translate
description: "Translate text into one or more target languages directly from its single source language: profile the genre, purpose, and tone, settle every term in the glossary, translate each target from the source (never through another translation; Chinese script variants excepted), localize instead of translating literally, protect code and placeholders, and verify completeness, terminology, and naturalness. Use when docs, UI strings, product or game content, or messages need a version in another language, or their source changed."
---
<!-- agentkit:generated from .ai/kit/core/skills/translate/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Translate

## Use When
- A text needs a version in another language, or its source changed and existing translations must follow.
- Localized variants in `docs.locales` must be brought in sync with their primary doc.

## Do Not Use When
- The source itself must be written or corrected → its owner first (docs: `project-docs-sync`; UI copy: the `ux-designer` role).
- The task is i18n plumbing (string extraction, locale loading, formatting libraries) → the surface owner's normal workflow.

## Inputs
- The source files or strings and their source language, per «Source Language» in [translation.md](.ai/kit/core/wiki/workflows/translation.md).
- Target locales as BCP 47 tags (for docs, `docs.locales`).
- The glossary at `docs.glossary`, and any style notes it records for this text type.

## Steps
1. Confirm the source language and that the source is final. When it is ambiguous or wrong, stop and send it back to its owner.
2. Build the content profile per «Content Profile»: genre, purpose and audience, register and tone, voice, constraints. Reuse the glossary's style notes when this text type already has them; otherwise record the per-locale choices there.
3. Extract names and domain terms from the source. Look each up in the glossary; add an entry for every new term with its decision (translate, transliterate, keep as-is) per «Terminology». Ask the user about names that need approval before using them.
4. For each target locale, translate directly from the source per «Direct From Source». Work section by section so nothing is skipped. When both Chinese scripts are targets, translate one and derive the other with the regional vocabulary review.
5. Localize per «Localization Over Literal Translation»: restructure for the target language, adapt idioms and references, and apply target conventions for dates, numbers, units, and punctuation.
6. Keep everything in «Protected Content» intact; point links at localized pages when they exist.
7. Verify each target per «Verification». For docs, also confirm the structure matches the primary per «Localized Variants» in [documentation.md](.ai/kit/core/wiki/workflows/documentation.md).
8. For files, write each target at its locale path (for docs, `docs.locale_pattern`) in the same unit as the source change.

## Output
- The translated files or strings per locale.
- Glossary entries and style notes added or changed.
- Items flagged for a human decision, with the reason for each.
