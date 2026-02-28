# Product Ideas Backlog

This backlog stores pre-spec product ideas in-repo.

Use this for ideas that are not ready for an OpenSpec change yet.

## Why this exists

- Avoids breaking OpenSpec validation with change stubs.
- Keeps idea history in git.
- Gives LLMs a predictable place and format for discovery and refinement.

## Structure

- `docs/backlog/index.md` - prioritized shortlist and status views.
- `docs/backlog/templates/idea-template.md` - canonical idea card template.
- `docs/backlog/ideas/*.md` - one file per idea.

## Status model

- `intake` - rough thought, not triaged.
- `triage` - reviewed, needs sizing/evidence.
- `validated` - enough confidence to prioritize.
- `ready` - ready to convert to OpenSpec change.
- `in-progress` - currently being specified or built.
- `done` - shipped.
- `icebox` - intentionally parked.

## Working rules

1. Capture every new idea as `docs/backlog/ideas/<kebab-case>.md`.
2. Keep one idea per file.
3. Update frontmatter fields before discussing priority.
4. Only create `openspec/changes/<name>/` when status is `ready`.
5. After creating an OpenSpec change, link the change in the idea file.

## LLM workflow

When using an assistant:

1. Read `docs/backlog/index.md` first.
2. Read top `ready` and `validated` ideas.
3. Propose ranking updates using impact, effort, confidence.
4. If promoting an idea to implementation, create OpenSpec artifacts and update idea status.
