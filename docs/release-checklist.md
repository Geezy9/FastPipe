# Pre-Publish Release Checklist

Use this checklist before cutting a release.

## Product Readiness

- [ ] README is current and links are valid.
- [ ] Documentation pages are updated for command/API changes.
- [ ] `help` output reflects current command set.

## Quality

- [ ] Build passes in Release mode.
- [ ] Test suite passes.
- [ ] Manual smoke test of core shell scenarios is complete.

## Packaging

- [ ] Native AOT publish succeeds for target runtime identifiers.
- [ ] Published binaries are validated on target OS.
- [ ] Version metadata is updated.

## Open Source Readiness

- [ ] License file is present.
- [ ] Contributing guide is up to date.
- [ ] Security policy is up to date.
- [ ] Contributor list is current.

## Release Artifacts

- [ ] Changelog/release notes are prepared.
- [ ] Tagged release is created.
- [ ] Installation/run instructions were validated from clean clone.
