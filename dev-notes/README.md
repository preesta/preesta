# dev-notes/

Engineering notes that intentionally stay out of the published docs site.

Anything here is for people working on Preesta itself — power-user knobs,
historical context for non-obvious code, and design notes that would only
confuse a rule author reading the public docs.

The MkDocs config publishes `docs/` only; this directory never reaches
the pages site. Linking from `docs/` into `dev-notes/` would produce a
broken external link — don't.
