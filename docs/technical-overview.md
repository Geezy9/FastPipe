# Technical Overview

This document describes KingShell internals and runtime behavior.

## Execution Model

KingShell supports two modes:

- **Interactive REPL**: starts a prompt, reads command lines, executes, renders output.
- **One-shot mode**: executes a single pipeline from CLI args and exits.

`Ctrl+C` behavior is two-stage:

1. Cancels active command execution.
2. Exits the session when idle.

## Pipeline Flow

Input goes through these stages:

1. Tokenize (quote/escape-aware)
2. Split segments by `|`
3. Parse literals into typed values
4. Dispatch each segment to a command
5. Pass prior segment output to next segment
6. Render final output

## Object Runtime

Pipeline values are wrapped in `BaseObj` / `BaseObj<T>`.

- Values are rented from type-specific pools.
- Command output is tracked per invocation.
- Pooled objects are released after rendering.

This design reduces steady-state allocations in REPL workloads.

## Rendering

Rendering uses Spectre.Console for:

- Banner and prompt styling
- Error/notice output
- Table rendering for structured file-system entries

## Structured Data Access

Member access for commands like `where`, `select`, `sort`, and serializers is contract-based:

- Prefer `IStructuredValue`
- Support string-keyed dictionaries
- Avoid runtime reflection-driven member discovery

This keeps behavior compatible with Native AOT constraints.
