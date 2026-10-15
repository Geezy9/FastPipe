# Contributing to KingShell

Thanks for your interest in contributing.

## Getting Started

1. Fork the repository.
2. Create a feature branch.
3. Make focused changes.
4. Add or update tests.
5. Submit a pull request.

## Development Setup

Requirements:

- .NET 10 SDK

Run locally:

```pwsh
dotnet restore
dotnet build
dotnet test
dotnet run
```

## Pull Request Expectations

- Keep PRs scoped to one concern.
- Include tests for behavior changes.
- Keep docs in sync with code changes.
- Use clear PR titles and summaries.

## Coding Guidelines

- Follow existing project style and patterns.
- Prefer minimal, maintainable changes.
- Preserve Native AOT compatibility constraints.

## Reporting Issues

When reporting a bug, include:

- Steps to reproduce
- Expected vs. actual behavior
- OS/runtime details
- Minimal repro command input where possible

## Security

Please report vulnerabilities through the process in [SECURITY.md](SECURITY.md).
