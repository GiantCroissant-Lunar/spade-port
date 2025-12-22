# Agent Adapters

This directory contains adapter implementations for integrating agents with external systems and platforms.

## Purpose

Adapters provide:

1. **Platform Integration** - Interface specifications for AI coding assistants (Windsurf, Claude, Copilot, Cursor)
2. **External Systems** - Integration with APIs, services, and tools (GitHub, CI/CD, databases)
3. **Workflow Format Conversion** - How to convert canonical workflows to platform-specific formats

## Types of Adapters

### Platform Adapters

Define how each AI platform works and integrates with the project:

- **`windsurf-adapter.yaml`** - Windsurf/Cascade configuration
- **`claude-adapter.yaml`** - Claude Code configuration
- **`copilot-adapter.yaml`** - GitHub Copilot configuration
- **`cursor-adapter.yaml`** - Cursor AI configuration

These adapters specify:

- Platform capabilities
- Configuration file locations
- Workflow format and invocation
- MCP support and features
- Integration commands

### External System Adapters

Connect agents to external tools and services:

- **`github-adapter.yaml`** - GitHub API integration (issues, PRs, checks)
- Additional adapters for CI/CD, databases, notification services

## File Formats

Adapters can be implemented as:

- `.yaml` - Configuration-based adapters (preferred)
- `.py` - Python adapter modules (for complex logic)
- `.js` / `.ts` - JavaScript/TypeScript adapters
- `.json` - JSON adapter definitions

## Platform Adapter Schema

```yaml
name: AdapterName
platform: platform-id
version: 1.0.0
type: coding_assistant | api | service

description: Brief description

capabilities:
  - capability1
  - capability2

configuration:
  rules_file: path/to/rules
  workflows_dir: path/to/workflows
  provider_config: path/to/provider/config

workflow_format:
  type: markdown | yaml | json
  extension: .md | .yaml | .cursorrules
  structure: |
    Example structure

invocation:
  workflows: how-to-invoke
  commands: how-to-execute

features:
  autonomous: true | false
  context_aware: true | false
  mcp_support: true | false
  model: model-name
  context_window: token-count

integration:
  command: |
    Setup commands
  verification: |
    Test commands
```

## Using Platform Adapters

### Generate Workflows

Workflows are stored canonically in `.agent/workflows/*.yaml` and converted to platform-specific formats:

```bash
# Generate for all platforms
python scripts/generate-workflows.py

# Generate for specific platform
python scripts/generate-workflows.py --platform windsurf

# Generate specific workflow
python scripts/generate-workflows.py --workflow build-and-test
```

### Check Platform Configuration

```bash
# Windsurf
ls .windsurf/workflows/
cat .windsurf/rules.md

# Claude
ls .claude/workflows/
cat CLAUDE.md

# Copilot
ls .github/copilot-workflows/
cat .github/copilot-instructions.md

# Cursor
ls .cursor/workflows/
cat .cursorrules
```

## Adding New Platform Adapters

1. **Create adapter YAML** in `.agent/adapters/platform-adapter.yaml`
2. **Document capabilities** and configuration
3. **Add generator** in `scripts/generate-workflows.py`
4. **Generate workflows** for the platform
5. **Test integration** with the platform

Example:

```yaml
name: NewPlatformAdapter
platform: newplatform
version: 1.0.0
type: coding_assistant

description: Adapter for NewPlatform AI

capabilities:
  - code_generation
  - command_execution

configuration:
  rules_file: .newplatform/rules.md
  workflows_dir: .newplatform/workflows/

workflow_format:
  type: markdown
  extension: .md

invocation:
  workflows: /workflow-name

features:
  autonomous: true
  mcp_support: true
```

## Relationship to Providers

- **Adapters** (`.agent/adapters/*.yaml`) - Platform capabilities and integration specs
- **Providers** (`.agent/providers/*.yaml`) - Platform-specific hints and optimizations
- **Workflows** (`.agent/workflows/*.yaml`) - Canonical workflow definitions

Together, these enable multi-platform agent support with a single source of truth.

## Related

- **Workflows**: See `.agent/workflows/SCHEMA.md` for workflow format
- **Providers**: See `.agent/providers/README.md` for provider hints
- **Generation**: See `scripts/generate-workflows.py` for conversion logic
