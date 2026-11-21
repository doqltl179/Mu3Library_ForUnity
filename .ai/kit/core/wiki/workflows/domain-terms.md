---
owns: "How project concepts are named: one canonical term per concept, its definition, the synonyms to avoid, and using the terms in code, docs, and conversation"
volatility: evolving
reviewed: 2026-10-23
---

# Domain Terms

A project concept carries one name everywhere: code, docs, issues, and conversation with the user. One name per concept keeps search reliable, shortens explanations, and exposes two people meaning different things by the same word.

## When

- naming a new type, module, function, file, setting, or doc section after a project concept,
- the user, an issue, or the code uses two words for one concept or one word for two,
- the user names a part of a user interface by its position or look instead of its name,
- adding or changing a glossary entry at `docs.glossary`.

## Route Away When

- how a term is rendered in another language: «Terminology» in [translation.md](translation.md),
- names of kit files, agents, and skills: [agent-first-writing.md](../principles/agent-first-writing.md),
- settling several open terms or decisions with the user: the `decision-interview` skill.

## Glossary Entries

- Each project concept has one entry in the glossary at `docs.glossary`: the canonical term, a one- or two-sentence definition of what it is, and the synonyms to avoid.
- Define the concept, not its implementation: no class names, tables, or file paths, which change while the concept stays.
- Define only concepts specific to the project or its domain. A general term (cache, timeout, retry) gets no definition or synonyms; it has a row only to fix its rendering per «Terminology» in translation.md.
- When several words compete for one concept, pick one and list the others as synonyms to avoid.
- The owner of the unit that settles a term writes its entry, in the same unit as the first code or doc that uses it. Renderings in other languages follow «Terminology» in translation.md.
- With `docs.glossary` empty, keep terms consistent within the change.

## Using Terms

- Use the canonical term in new names, docs, issues, commit messages, and reports. Keep an existing identifier that uses another word unless renaming it is the request; renaming a public name follows «Compatibility» in [code-changes.md](code-changes.md).
- When the user or a document uses a synonym to avoid, write the canonical term in what you produce.
- When the user names a part of a user interface by its position or look (for example "the button at the top left"), find the candidate screen in the feature map («Project Wiki» in [project-overlay.md](../integration/project-overlay.md)) when the project has one, identify the part from the file that the entry cites or by reaching the screen with the entry's steps in the running app, and write the part's canonical term. Ask which part is meant when more than one still matches.
- When one word could mean two concepts and the choice changes the result, ask which is meant per «Stop, Pause, or Proceed» in [integrity.md](../principles/integrity.md), then record the answer in the glossary.
