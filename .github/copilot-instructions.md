# Copilot instructions for ChatGPTBot

## Build, run, and test

```powershell
# Full solution
dotnet build ChatGPTv3.slnx

# Individual projects
dotnet build ChatGPTv3.Core\ChatGPTv3.Core.csproj
dotnet build ChatGPTv3.OpenAIClient\ChatGPTv3.OpenAIClient.csproj
dotnet build ChatGPTv3.UI\ChatGPTv3.UI.csproj

# Standalone UI debug entry point
dotnet run --project ChatGPTv3.UI\ChatGPTv3.UI.csproj
```

- No lint command is configured in this repository.
- There is no formal test project. `ChatGPTv3.OpenAIClient\SseParserTests.cs` contains self-contained parser tests, but they must be invoked from a temporary console host or a debugger because `ChatGPTv3.OpenAIClient` is a library.
- To run the full parser test set, call `ChatGPTv3.OpenAIClient.SseParserTests.RunAll()`.
- To run a single parser test, call one specific method such as `ChatGPTv3.OpenAIClient.SseParserTests.TestToolCallStreaming()`.

## Architecture overview

- This repository is an **AMN2 plugin**, not a standalone bot app. `ChatGPTv3.Core\Entry.cs` is the plugin entry point and wires the system manually in `OnEnableAsync()`.
- The solution has three layers:
  - `ChatGPTv3.OpenAIClient`: custom `net9.0` HTTP/SSE client for chat completions, embeddings, streaming deltas, and tool-call parsing.
  - `ChatGPTv3.Core`: the actual plugin, including config, SQLite models, prompt building, memory, mood, scheduling, MCP integration, and chat execution.
  - `ChatGPTv3.UI`: WPF management panel that references Core and can run either inside AMN2 (`MenuEntry`) or standalone (`TesterEntry` + `UIBootstrap`).
- Group/private chat handling is built as an ASP.NET-style middleware pipeline. `ChatCommands` is the sole message handler; it creates `ChatContext` and dispatches into `ChatPipelineBuilder` pipelines assembled in `Entry.OnEnableAsync()`.
- The main group flow is: access control -> message filter -> concurrency gate/interrupt -> reply decision -> image resolution -> quoted-message resolution -> message persistence -> chat execution/background counters.
- `ChatService` runs the LLM turn loop. It builds requests from `PromptBuilder`, optionally exposes MCP tools, executes up to 10 tool-call rounds, then records tool results for post-turn summarization.
- Prompt construction is intentionally split for cacheability: fixed behavior/rules stay in the `system` message, while time, mood, diary, schedule, knowledge, and chat history go into `<system_dynamic_data>` inside the final user message.
- Memory is split by purpose:
  - diary memory: push-style natural-language per-group memory
  - knowledge memory: Qdrant-backed retrieval
  - context compression: background summaries folded back into history reads
- MCP support has two branches: built-in tools implemented in Core and external HTTP/stdin-stdout MCP clients managed by `MCPClientManager`.

## Repository-specific conventions

- Follow `CLAUDE.md` for repository architecture and `llms.txt` for **AMN2 framework rules**. `llms.txt` is the authoritative reference for plugin-specific APIs, file locations, handler behavior, and WPF-in-plugin pitfalls.
- Core code uses **static subsystems with manual wiring**, not a DI container. Before introducing new services or stateful components, check whether an existing static manager should own that responsibility.
- Treat `ChatCommands` as the only `CommandHandlerBase` message entry. AMN2 only dispatches one handler per interface type, so adding another competing message handler can break routing.
- Preserve the **interrupt instead of busy-lock** behavior in `UseConcurrencyGate()`: a newer message for the same group/private context cancels the in-flight request and replaces it.
- All plugin-generated files must live under `API.AppApi.GetAppDirectory()`. Do not write plugin data to the current working directory or `AppDomain.BaseDirectory`.
- When sending local images/audio through AMN2, the file must already exist under the framework media directories (`data\image`, `data\record`), and code should use `MessageBuilder` rather than hand-written CQ codes.
- `Config.json` stores configuration and API key references, but the actual API keys live in SQLite (`APIKey` table). Do not assume secrets are only in JSON files.
- Database access uses SqlSugar CodeFirst, and the established pattern is **one fresh `SqlSugarClient` per operation** via `SQLiteManager`, not a shared long-lived context.
- If you edit prompt generation, keep the fixed `system` prompt cache-friendly and put volatile context into the final user message instead of moving everything into `system`.
- The WPF panel must run on its own STA thread with its own dispatcher. Reuse the `MenuEntry` pattern instead of opening WPF windows directly on the AMN2 framework UI thread.
- AMN2 plugin projects require exact package versions and the AMN2 loading properties already used here (`CopyLocalLockFileAssemblies`, `EnableDynamicLoading`). Do not switch to floating package versions when changing project files.
