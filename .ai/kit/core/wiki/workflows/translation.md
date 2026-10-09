---
owns: "Translation policy for any text (docs, UI strings, product and game content, messages): the source language, direct-from-source translation, content profiling, localization over literal translation, terminology consistency, protected content, and verification"
volatility: evolving
reviewed: 2026-10-09
---

# Translation

Translation here means producing text in another language that reads as if it had been written in that language for the same readers and purpose. The `translate` skill walks the steps; this page owns the rules.

## When

- producing or updating any text in a language other than its source: localized docs, UI strings, store listings, game or product content, messages.

## Route Away When

- which documents must have localized variants and when they are updated: «Localized Variants» in [documentation.md](documentation.md),
- writing the source text itself: `technical-writer` for docs, `ux-designer` for UI copy.

## Language Tags

Name languages with BCP 47 tags (`en`, `ko`, `ja`, `zh-Hans`, `zh-Hant`, `pt-BR`), and use the same tags in `docs.locales`, file paths, and the glossary.

## Source Language

- Every translatable text has exactly one source language: `docs.source_locale` in the profile, or `project.language` when that key is empty. A text written in another language states its source explicitly.
- Translate only from a final source. When the source changes, update every target from the new source; never patch one target and let the others drift.
- When the source is wrong or ambiguous, fix or clarify the source first; never repair meaning only in a translation.

## Direct From Source

- Translate every target directly from the source. Never chain through another translation (for example `en → ko → ja`): each hop adds interpretation, and errors compound.
- Exception, Chinese scripts: when both `zh-Hans` and `zh-Hant` are targets, translate one directly from the source and derive the other by script conversion. Script conversion alone is not enough: then review regional vocabulary and conventions for the target region (for example 软件/軟體, 视频/影片, 信息/資訊, and punctuation), because Mainland, Taiwan, and Hong Kong usage differ.
- A translation used as an intermediate for reading only (for example to understand a source you cannot read) is never shipped.

## Content Profile

Before translating, classify the text and keep the profile with the work:

| Dimension | Examples |
|---|---|
| Genre | Technical docs, UI strings, marketing, news, report, fiction, game dialogue or items, chat, legal |
| Purpose and audience | Instruct developers, persuade buyers, entertain players, inform the public |
| Register and tone | Formal, neutral, friendly, playful, humorous, serious, urgent |
| Voice | A brand voice, a narrator, a character's speech habits |
| Constraints | UI length limits, line breaks, character limits, legal fixed wording |

Carry the profile into each target with that language's own means, not the source's surface form: for example Korean speech levels (합니다체, 해요체, 반말), Japanese politeness (です・ます体, だ・である体, 敬語), or the formality of `vous`/`tu` and `Sie`/`du`. Record the choice per text type and locale in the glossary's style notes so later translations keep it.

## Localization Over Literal Translation

- Translate meaning, intent, and effect, not words or sentence structure. Restructure sentences, reorder clauses, and split or merge sentences as the target language needs.
- Replace idioms, jokes, wordplay, and cultural references with ones that work for the target readers; keep the original only when it is the point.
- Use current, natural usage of the target audience: the wording a native writer of that genre would use today. Avoid translationese, calques, and outdated expressions; avoid slang that will date quickly unless the profile calls for it.
- Follow target conventions for dates, numbers, currency, units, names, addresses, quotation marks, and punctuation.
- Never add, drop, or soften information. Localization changes how something is said, never what is claimed.

## Terminology

- The glossary at `docs.glossary` is the single owner of how terms are rendered: one row per concept, one approved rendering per locale.
- Look up every name and domain term before translating it. A term with no entry gets one before its first use, with the decision: translate, transliterate, or keep as-is.
- Within one language, the same concept always uses the same approved rendering, in every file and every later translation. Example: once the item `냥이` is rendered `Nyani` in English, it is never `Kitty` elsewhere.
- Changing an approved rendering is one change: update the glossary entry and every occurrence in that language in the same unit.
- Names with product, legal, or brand impact (product names, character names, marketed features) need the user's approval before first use.

## Protected Content

Never translate or alter: code, commands, identifiers, file paths, URLs, configuration keys, placeholders and format specifiers (`{name}`, `%s`, `{{count}}`), markup and its attributes, and terms the glossary marks keep-as-is. Translate link text, but point links at the matching localized page when one exists.

## Verification

1. Completeness: every sentence, list item, table cell, and string of the source has a counterpart; nothing was added.
2. Terminology: every glossary term in the source appears with its approved rendering.
3. Protected content: placeholders, code, and links are intact, and counts match the source.
4. Naturalness: reread each target as a native reader of that genre, without looking at the source; rewrite anything that reads as a translation.
5. Flag what still needs a human decision (ambiguous sources, brand names, legal wording) in the report instead of guessing. Back-translation is a spot check, never the quality bar.
