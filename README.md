# FastPipe

Typed, Native AOT shell for .NET that pipes objects instead of plain text.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Contributions Welcome](https://img.shields.io/badge/contributions-welcome-brightgreen.svg)](CONTRIBUTING.md)

FastPipe is an interactive shell built on .NET 10 with a typed pipeline, low-allocation runtime, and plugin support.

## Highlights

- Typed object pipeline (`BaseObj`) across commands.
- Native AOT-friendly design (no runtime reflection-based command model).
- Quote-aware tokenizer and typed literals.
- External plugin model using process-based executables.
- Clean REPL experience with cancellation (`Ctrl+C`).

## Quick Start

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```pwsh
git clone https://github.com/Geezy9/FastPipe
cd FastPipe
dotnet run
```

Run one pipeline and exit:

```pwsh
dotnet run -- "echo hello | upper"
```

Publish Native AOT binary:

```pwsh
dotnet publish -c Release -r win-x64
```

## Built-in Command Groups

- Core: `help`, `echo`, `clear`, `exit`
- File system: `pwd`, `cd`, `ls`, `mkdir`, `copy`, `move`, `remove`, `touch`, ...
- Arithmetic/comparison: `add`, `sub`, `mul`, `div`, `eq`, `gt`, ...
- Logic: `and`, `or`, `not`
- Data: `where`, `select`, `map`, `sort`, `take`, `group-by`, `uniq`, `split`, `flatten`, `contains`, `replace`, `min`, `max`, `sum`, `count`, `get-properties` (`props`)
- Serialization: `sjson`, `syml`, `scsv` (aliases like `to-json`, `to-yaml`, `to-csv`)
- Deserialization: `djson`, `dyml`, `dcsv` (aliases like `from-json`/`as-json`, `from-yml`/`as-yml`, `from-csv`/`as-csv`)

Use `help` in the shell for the canonical command list and usage.

Example round-trip:

```pwsh
echo '{"name":"alpha","count":2}' | djson | sjson
```

## Documentation

Technical details were moved out of the README for easier navigation:

- [Technical Overview](docs/technical-overview.md)
- [Command Development API](docs/command-development.md)
- [Plugin System](docs/plugin-system.md)
- [Release Checklist](docs/release-checklist.md)

## Contributing

Contributions are welcome. Start here:

- [Contributing Guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security Policy](SECURITY.md)

Good first contribution ideas:

- Add a focused built-in command.
- Improve cross-platform shell behavior.
- Add tests around parsing, pipelines, or command behavior.
- Improve documentation and usage examples.

## Contributors

Thanks to everyone building FastPipe.

- [@Geezy9](https://github.com/Geezy9)

Want to be listed? Open a pull request.

Full list: [CONTRIBUTORS.md](CONTRIBUTORS.md)

## License

This project is licensed under the [MIT License](LICENSE).
