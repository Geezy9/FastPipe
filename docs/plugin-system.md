# Plugin System

KingShell uses a process-based plugin model.

## Why Process-Based

KingShell is designed for Native AOT. Instead of runtime assembly loading, plugins execute as child processes.

Benefits:

- AOT-friendly extension model
- Plugin language/runtime independence
- Plugin crashes are isolated from shell process

## Extension Paths

1. **Registered plugins** via `plugins/<plugin-name>/plugin.json`
2. **Ad-hoc executables** resolved from working directory or `PATH`

## Manifest Format

Example `plugin.json`:

```json
{
  "name": "reverse",
  "aliases": ["rev"],
  "description": "Reverses each supplied string.",
  "usage": "reverse <text...>",
  "executable": "reverse.exe",
  "arguments": ["--mode", "text"]
}
```

Fields:

- `name` (required)
- `description` (required)
- `usage` (required)
- `executable` (required)
- `aliases` (optional)
- `arguments` (optional)

## Input/Output Behavior

- Explicit command args are forwarded to plugin process.
- Piped input is sent on stdin, line-by-line.
- Stdout lines become pipeline string output.
- Non-zero exit code is treated as command failure.

## Cross-Platform Notes

- Windows supports `.exe`, `.cmd`, `.bat`, `.com` resolution behavior.
- Linux/macOS support extensionless executables.
- Plugin working directory is the manifest directory.
