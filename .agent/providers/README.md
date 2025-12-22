# Providers

This directory contains provider-specific hints and configurations for different AI agent platforms.

## Overview

Different AI agent platforms (Claude, GitHub Copilot, etc.) may have different capabilities, context limits, or preferred interaction patterns. Provider-specific hints help agents leverage platform-specific features while maintaining consistent behavior.

## Provider Files

- **claude.yaml** - Configuration and hints for Claude Code and Claude-based agents
- **copilot.yaml** - Configuration and hints for GitHub Copilot agents
- **windsurf.yaml** - Configuration and hints for Windsurf agents (GPT-5.1)

## Purpose

Provider hints are **optional** and serve to:

1. **Optimize context usage**: Leverage platform-specific context windows
2. **Enable platform features**: Use MCP servers, custom tools, or APIs
3. **Adjust behavior**: Tune verbosity, formatting, or interaction patterns
4. **Document capabilities**: Make platform-specific features discoverable

## Provider Configuration Format

```yaml
name: ProviderName
platform: claude | copilot | other
version: 0.1.0

capabilities:
  context_window: 200000 # tokens
  supports_mcp: true
  supports_tools: true
  max_tool_calls: 50

preferences:
  verbosity: normal | verbose | minimal
  code_format: markdown | plain
  error_handling: detailed | concise

hints:
  - 'Use MCP servers for external data access'
  - 'Leverage extended context for large codebases'
  - 'Batch file reads for efficiency'
```

## Example: Claude Provider (claude.yaml)

```yaml
name: ClaudeProvider
platform: claude
version: 0.1.0

capabilities:
  context_window: 200000
  supports_mcp: true
  supports_tools: true
  max_tool_calls: 50
  max_output_tokens: 4096

preferences:
  verbosity: normal
  code_format: markdown
  error_handling: detailed
  thinking_process: visible

hints:
  - 'Use extended thinking for complex problems'
  - 'Leverage MCP servers for Git, filesystem, and external APIs'
  - 'Batch independent tool calls for efficiency'
  - 'Use sequential thinking tool for multi-step reasoning'
  - 'Surface reasoning and trade-offs in responses'

skill_execution:
  - 'Load skill entry file first (SKILL.md)'
  - 'Only load reference files when specific procedure needed'
  - 'Progressive disclosure: start minimal, expand as needed'

optimization:
  - 'Prefer parallel tool calls for independent operations'
  - 'Cache frequently accessed files in working memory'
  - 'Use git diff before full file reads when checking changes'
```

## Example: Copilot Provider (copilot.yaml)

```yaml
name: CopilotProvider
platform: copilot
version: 0.1.0

capabilities:
  context_window: 128000
  supports_mcp: false
  supports_tools: true
  max_tool_calls: 30

preferences:
  verbosity: concise
  code_format: markdown
  error_handling: concise
  thinking_process: hidden

hints:
  - 'Keep responses focused and actionable'
  - 'Prioritize code snippets over explanations'
  - 'Use GitHub-specific tools when available'
  - 'Minimize token usage for faster responses'

skill_execution:
  - 'Load only necessary context'
  - 'Prefer direct execution over explanation'
  - 'Surface errors quickly for rapid iteration'

optimization:
  - 'Batch file operations when possible'
  - 'Use targeted file reads (line ranges)'
  - 'Minimize repeated tool calls'
```

## When to Use Provider Hints

### Use Provider Hints When

- Platform has unique capabilities (MCP servers, custom tools)
- Context window size differs significantly
- Interaction patterns need adjustment
- Platform-specific optimizations available

### Don't Use Provider Hints For

- Core agent logic (belongs in agent definitions)
- Skills (belongs in skill definitions)
- Policies (belongs in policy files)
- General best practices (belongs in documentation)

## Provider-Agnostic Design

The agent infrastructure is designed to work across all platforms. Provider hints are **optional optimizations**, not requirements.

Core files (agents, skills, schemas, policies) should be **provider-agnostic**.

```yaml
# ✓ GOOD: Provider-agnostic skill
name: dotnet-build
kind: cli
description: Build .NET solution using dotnet CLI

# ✗ BAD: Provider-specific in core skill
name: dotnet-build
claude_optimization: use_mcp_server  # Don't do this
```

## Adding a New Provider

1. Create `your-provider.yaml` in `.agent/providers/`
2. Document platform capabilities
3. Add platform-specific hints
4. Test that core functionality works without hints
5. Document provider in this README

## Provider Detection

Agents can detect which platform they're running on and load appropriate hints:

```python
# Example: Auto-detect provider
import os
from pathlib import Path
import yaml

def detect_provider():
    if os.getenv('ANTHROPIC_API_KEY'):
        return 'claude'
    elif os.getenv('GITHUB_TOKEN'):
        return 'copilot'
    else:
        return 'unknown'


def load_provider_hints(provider_name):
    hints_path = f".agent/providers/{provider_name}.yaml"
    if Path(hints_path).exists():
        with open(hints_path, encoding='utf-8') as f:
            return yaml.safe_load(f)
    return {}
```

## Best Practices

1. **Keep optional**: Core functionality must work without provider hints
2. **Document capabilities**: Make platform features discoverable
3. **Avoid lock-in**: Don't make agents dependent on specific platforms
4. **Test across platforms**: Verify behavior on multiple providers
5. **Version carefully**: Track when platform capabilities change

## Platform Comparison

| Feature          | Claude         | Copilot       | Windsurf      | Universal |
| ---------------- | -------------- | ------------- | ------------- | --------- |
| Context Window   | 200K tokens    | 128K tokens   | 128K tokens   | -         |
| Model            | Sonnet 4.5     | GPT-4         | GPT-5.1       | Varies    |
| MCP Support      | Yes            | Yes           | Yes           | Varies    |
| Tool Calling     | Yes (extended) | Yes (limited) | Yes           | Yes       |
| Thinking Process | Visible        | Hidden        | Minimal       | Varies    |
| Code Execution   | Via MCP/tools  | Via MCP/tools | Via MCP/tools | Varies    |
| Autonomy Level   | Normal         | Concise       | High          | Varies    |

## Future Providers

Placeholder for future AI agent platforms:

- **Azure OpenAI** - Azure-hosted models with enterprise features
- **Custom agents** - Self-hosted or custom agent implementations
- **Multi-agent systems** - Coordinated agent orchestration

## Related

- **Agents**: See `.agent/agents/README.md` for agent definitions
- **Skills**: See `.agent/skills/README.md` for skill definitions
- **RFC-004**: Agent Infrastructure Enhancement design document
