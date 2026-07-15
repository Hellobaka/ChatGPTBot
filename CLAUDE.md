# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ChatGPT QQ Bot v3 — a **plugin for the Another-Mirai-Native v2 (AMN2) QQ bot framework**. It provides ChatGPT-powered group/private chat with streaming responses, push-mode diary memory, natural-language mood, cron scheduled tasks, MCP tool calling (built-in + external), image scraping, and Qdrant knowledge retrieval.

**Solution:** `ChatGPTv3.slnx` (the newer .slnx format — `dotnet build` or Visual Studio 2022+). Three projects:

```
ChatGPTv3.OpenAIClient   net9.0          HTTP/SSE transport lib (Library)
        ▲
ChatGPTv3.Core           net9.0-windows  Bot plugin + all subsystems (Library, AssemblyName=ChatGPTv3)
        ▲
ChatGPTv3.UI             net9.0-windows  WPF + HandyControl management panel (WinExe)
```

`llms.txt` at the repo root is the **AMN2 plugin authoring reference** (project setup, event handling, `MessageBuilder`, WPF windows, pitfalls). Consult it for anything framework-specific.

## Build & Run Commands

```bash
# Build the entire solution (all 3 projects)
dotnet build ChatGPTv3.slnx

# Build individual projects
dotnet build ChatGPTv3.Core/ChatGPTv3.Core.csproj
dotnet build ChatGPTv3.OpenAIClient/ChatGPTv3.OpenAIClient.csproj
dotnet build ChatGPTv3.UI/ChatGPTv3.UI.csproj

# Run the management UI standalone (no AMN2 host) for debugging.
# Opens the WPF panel via TesterEntry.Main(); bootstraps Core subsystems through UIBootstrap.
# Requires a writable app directory (Config.json / core.db are created on first run).
dotnet run --project ChatGPTv3.UI/ChatGPTv3.UI.csproj
```

There is **no test runner project**. `ChatGPTv3.OpenAIClient/SseParserTests.cs` contains a self-contained `RunAll()` (no network needed), but the project is now a `Library` and its former `Program.cs` host was removed — so the tests currently have no entry point. To run them, call `SseParserTests.RunAll()` from a temporary console host or a debug session.

The bot plugin itself is not runnable standalone — it is a class library dynamically loaded by the AMN2 host. `Entry.cs` (in Core) is the `PluginBase` entry point.

## Project Architecture

### ChatGPTv3.Core — The Bot Plugin

**Not a standalone app.** Class library targeting `net9.0-windows`, dynamically loaded by the AMN2 host. Entry point: `Entry.cs` inheriting `PluginBase`.

**Key design decisions:**
- **No DI container.** Everything uses static classes with manual wiring in `Entry.OnEnableAsync()`. State is set via property injection (e.g., `CommonHelper.LogInfo = API.Logger.Info`).
- **Custom OpenAI client** instead of Microsoft.Extensions.AI + OpenAI SDK. `ChatGPTv3.OpenAIClient` handles all HTTP/SSE parsing directly with zero external HTTP dependencies beyond `System.Net.Http`.
- **Real MCP SDK for external clients.** Core references `ModelContextProtocol.AspNetCore`; `MCPExternalClient` (Http/Stdio) wraps the SDK's `McpClient`. Built-in tools are custom (`MCPCustomClient` + `MCPSelfBuiltinTools`).
- **SQLite via SqlSugarCore ORM** (CodeFirst). Each DB operation creates its own `SqlSugarClient` — no shared connection or unit-of-work pattern.

**Startup wiring (`Entry.OnEnableAsync`):** captures bot identity → loads config → creates DB → validates chat API keys → fires a background `Task.Run` that initializes subsystems (MoodState, SchedulerManager, ScheduledTaskRunner, MemoryManager, DiaryMemoryManager, ContextCompressor, MCP) → subscribes to config hot-reload → **builds the chat pipelines**:

```csharp
ChatCommands.GroupPipeline = new ChatPipelineBuilder()
    .UseAccessControl()
    .UseMessageFilter()
    .UseConcurrencyGate()      // interrupt + debounce
    .UseReplyDecision()
    .UseMessageImageResolver()
    .UseMessageReferenceResolver()
    .UseMessageRecorder()
    .UseChatHandler()          // = UseBackgroundCounters + UseChatExecutor
    .Build();
```

**Message processing flow (the critical path) — an ASP.NET-style middleware pipeline:**
```
AMN2 dispatches message
  → ChatCommands.OnNoMatchAsync()           (sole handler; [Command]/[DynamicCommand] take priority)
    → builds ChatContext, invokes GroupPipeline / PrivatePipeline (static Func<ChatContext,Task>)
      1. UseAccessControl        — whitelist/blacklist (group or QQ)
      2. UseMessageFilter        — drop empty/non-image
      3. UseConcurrencyGate      — per-context CTS + debounce; a newer message CANCELS the in-flight call
      4. UseReplyDecision        — ReplyManager willingness + optional LLM check + RNG gate
      5. UseMessageImageResolver — download/describe images via ImageScraper (vision)
      6. UseMessageReferenceResolver — resolve quoted bot messages from DB (cross-turn reference)
      7. UseMessageRecorder      — persist user message to ChatRecord
      8. UseChatHandler:
           · UseBackgroundCounters — tick DiaryMemoryManager + ContextCompressor per message
           · UseChatExecutor       — DoChatAsync: build prompt → ChatService (tool-call loop) →
                                     reply-dedup → send (split + emoji) → persist + tool-call logging
```

**Interrupt mechanism (implemented in `UseConcurrencyGate`):** keys on GroupId (or QQ for private), stores a `(CancellationTokenSource, version)`. A new message for the same context cancels the previous CTS and bumps the version. After an optional debounce delay (`MessageDebounceMs`), if the entry is no longer the latest version it exits early (discarded as superseded). The surviving entry's `CancellationToken` flows into `ChatService.GetChatResultAsync` and the SSE stream, so an in-flight LLM call is cancelled mid-stream when a follow-up arrives — v2's busy lock silently discarded follow-ups; v3 restarts with updated context.

**Prompt caching strategy** (`PromptBuilder`):
- Fixed system prompt (identity, rules, tool instructions) → `system` role — cacheable.
- Dynamic content (time, mood, schedule, knowledge, diary, chat history) → wrapped in `<system_dynamic_data>` inside the LAST `user` message. Keeps the system message cache-hit friendly.

**Tool call loop** (`ChatService`):
- Up to 10 rounds of LLM ↔ tool execution.
- `ToolCallService` enforces per-identity rate limits (`MaxToolCallCountEachTurn`, `AbortToolCallCountEachTurn`).
- `MCPClientManager.GetToolsForConversation(MCPToolContext)` aggregates tools from all enabled clients (filtered by group/QQ/master permissions), applying per-client `ToolNameConverters` for name mapping.
- After the turn, `chatService.ToolCallLog` is summarized by `ToolResultSummarizer` (fire-and-forget) into `ChatRecord.ParsedMessage`.

**API key selection:** Each purpose (chat, reply-check, diary, embedding, splitter, image-describer, rerank) has a list of key IDs in `Config.json`. Keys are randomly selected per call (`OrderBy(_ => Guid.NewGuid())`) for load distribution. Each purpose falls back to chat keys when its own list is empty.

### ChatGPTv3.OpenAIClient — HTTP/SSE Transport

Pure `net9.0` class library (`OutputType=Library`). Zero external HTTP dependencies beyond `System.Net.Http`.

Core types:
- `OpenAiChatClient` — `CompleteAsync()` (non-streaming), `StreamAsync()` (IAsyncEnumerable SSE), embeddings
- `SseResponseParser` — custom SSE parser handling incremental tool-call argument merging
- `ChatMessage` — factory methods: `System()`, `User()`, `Assistant()`, `Tool()`, `UserWithParts()`, `AssistantWithToolCalls()`
- `ChatCompletionRequest` — supports JSON mode, `Tools`, `ToolChoice`, stream-options usage tracking
- `ChatCompletionResponse`, `ContentPart`, `StreamingUpdate`, `TokenUsageInfo`, `ToolDefinition`, `ToolCallRequest`

Serialization uses `SnakeCaseLower` naming policy matching OpenAI's API convention. Built-in MCP tools build their JSON schemas with a local `Schema(...)`/`MakeTool(...)` helper in `MCPSelfBuiltinTools`.

### ChatGPTv3.UI — WPF Management Panel (actively being built)

WPF (`UseWPF`) + **HandyControl** 3.5.1 + MahApps icon pack + CommunityToolkit.Mvvm (MVVM). `net9.0-windows`, `OutputType=WinExe`. References Core. Shipped alongside the plugin assembly.

Two entry points:
- **`MenuEntry` (`IMenuHandler`, `[Menu("ChatGPTv3 管理面板")]`)** — AMN2-hosted entry. Creates `MainWindow` on a **dedicated STA thread** with its own `Application` + `Dispatcher.Run()` loop (the framework UI thread is WinForms and breaks WPF keyboard input — see `llms.txt` §8.4). HandyControl theme dictionaries are merged manually into `Application.Resources` (no `App.xaml` in a plugin). Hide-on-close reuses the window across menu clicks.
- **`TesterEntry.Main()`** — standalone WinExe debug entry. Calls `UIBootstrap.Initialize()` (replicates `Entry.OnEnableAsync` init without the AMN2 host) then opens `MainWindow`. For designing/testing the panel without launching the bot.

Views: `ConfigurationView` (settings/API keys), `ChatTestView` (chat/MCP testing). ViewModels under `ViewModels/` inherit `ViewModelBase`.

## Key Subsystems

| Subsystem | Location | Role |
|-----------|----------|------|
| **Pipeline** | `Commands/ChatPipelineBuilder.cs`, `PipelineMiddleware.cs` | Composable middleware (built once at startup; `ChatContext` flows through it). Extension methods in `PipelineMiddlewareExtensions` |
| **MCP** | `Model/MCP/` | ~30 built-in custom tools (`MCPSelfBuiltinTools`) + external Http/Stdio clients via real MCP SDK. `MCPClientManager` aggregates per-conversation; permissions + name mapping; heartbeat + 3-retry reconnect then auto-disable |
| **Memory (Diary)** | `Model/DiaryMemoryManager.cs` | Push-mode: LLM writes a natural-language diary per group, injected into the next prompt. Dual trigger (message-count threshold + time-interval timer). Side-effect: updates `MoodState` |
| **Memory (Knowledge)** | `Model/MemoryManager.cs` | Qdrant-backed semantic search only — short-term/long-term/ToDo memory was removed in v3 |
| **Mood** | `Model/MoodState.cs` | Natural-language mood (replaces v2 valence/arousal math). Updated by LLM `UpdateMood` tool or diary side-effect; time-weakened via wording |
| **Reply** | `Model/ReplyManager.cs` | `willingness = attention × timing × activity`. No timers, no decay, state only changes on message arrival. Optional LLM borderline check (`CheckByLLM`) |
| **Scheduler** | `Model/SchedulerManager.cs` | LLM-generated daily schedule JSON; bot self-adjusts via `UpdateSchedule`/`GetCurrentSchedule` tools |
| **Scheduled Tasks** | `Model/ScheduledTaskRunner.cs`, `DB/ScheduledTask.cs`, `Model/CronHelper.cs` | Cron-based proactive tasks polled every 30s; runs with a task-mode system prompt (persona + proactive rules) + full MCP tool chain |
| **Context Compression** | `Api/ContextCompressor.cs`, `DB/ContextSummary.cs` | Background LLM summarization of old messages; `ChatRecord.GetGroupHistory` transparently prepends the covering summary |
| **Image** | `Model/ImageScraper.cs` | Download, vision description, emoji detection; `DescribeImage`/`AddPictureToContext` MCP tools feed native multimodal input |
| **Tool Summary** | `Api/ToolResultSummarizer.cs` | Post-hoc LLM summary of tool-call results into `ChatRecord.ParsedMessage` (fire-and-forget) |
| **Per-Group Config** | `DB/GroupConfig.cs` | Per-group custom prompt / nicknames overriding global `AppConfig` |
| **Config** | `Config/` | `ConfigManager` — JSON persistence + `FileSystemWatcher` hot-reload. `AppConfig` — ~100+ typed properties |

## Database (SQLite via SqlSugarCore)

File: `core.db` in the app directory (`API.AppApi.GetAppDirectory()`). Tables (CodeFirst, `SQLiteManager.CreateDB`):

- `ChatRecord` — message history. `SenderType` (User/Assistant/Tool); `ParsedMessage` is TEXT (for Tool records: LLM summary of the result). Tool fields: `HasToolCalls`, `ToolCallId`, `ToolName`, `IsToolSuccess`. `GetGroupHistory` transparently merges the covering `ContextSummary`.
- `APIKey` / `LLMModelConfig` — API endpoint + model config (OneToMany). **Real API keys live here**, not in Config.json. `LLMModelConfig` carries pricing fields for cost tracking.
- `TokenUsage` — token consumption + cost tracking (`UsageTracker`).
- `Picture` — image/emoji metadata cache (FilePath, Hash, Description, IsEmoji).
- `Relationship` — user favorability scores (kept from v2; updated via `UpdateFavorability` tool).
- `ScheduledTask` — cron-based proactive tasks (`CronExpr`, `NextFireAt`, `TargetType`, `ExtraPrompt`).
- `GroupConfig` — per-group overrides (custom prompt, nicknames).
- `ContextSummary` — compressed summaries covering older message ranges (`FindCoveringBefore`).

## Configuration

- `Config.json` — main config (app dir). Hot-reloaded via `FileSystemWatcher`; `OnConfigReloaded` re-runs `AppConfig.Init()`.
- `MCP.json` — MCP client definitions (built-in tool toggles + external Http/Stdio clients). Loaded/saved by `MCPClientManager`; polymorphic via `JsonDerivedType` discriminators (`Custom`/`Http`/`Stdio`).
- `schedules.json` — LLM-generated daily schedule.
- `diary_<groupId>.json`, `diary_state.json` — diary entries + per-group message counters / last-diary timestamps.

## v3 Design Philosophy

v3 is a **redesign, not a blind port** of v2. The v2 valence/arousal mood model, short/long-term memory + Qdrant extraction chain, and hour-block schedules were all found to add little real value and were rethought. These redesigns are now **implemented**:

- Memory → push-mode diary (no pull retrieval for conversation memory; Qdrant retained only for knowledge).
- Mood → natural language, dual-trigger, time-weakened by wording.
- Schedule → kept v2 daily generation, added self-update MCP tools.
- Reply → no-timer three-dimension willingness.
- ChatPipeline → ASP.NET-style middleware with CTS-based interrupt.
- Plus: cron scheduled tasks, context compression, image scraping, tool-result summarization, per-group config, reply dedup, cross-turn reference, content-filter fallback.

Design rationale for each lives in the user's memory docs (e.g. `[[memory-system-diary]]`, `[[mood-system-redesign]]`, `[[reply-manager-redesign]]`, `[[pipeline-refactor-plan]]`, `[[database-redesign]]`). When changing these systems, consult the corresponding memory doc for the reasoning behind the current shape — and **discuss with the user before large structural departures** from these designs.

## Important Patterns & Gotchas

- **API keys ARE stored in the database (`APIKey` table)**, not just in Config.json. Config.json only stores key *references* (Id + Name). Real keys live in SQLite.
- **Qdrant is a separate REST-based vector DB**, not part of SQLite. Configured via `QdrantHost`/`QdrantPort`/`QdrantAPIKey`. Used only for the knowledge collection now — not for conversation memory.
- **Crypto-grade RNG** (`CommonHelper.Next()`/`NextDouble()`) uses `RandomNumberGenerator.Fill()` — not `System.Random`. Important for reply probability and key selection.
- **Interrupt, not busy-lock.** A follow-up message for a busy context cancels the in-flight LLM call (CTS) and restarts with updated context — it is not silently discarded. The debounce window (`MessageDebounceMs`) coalesces rapid consecutive messages; superseded entries are dropped.
- **WPF UI must run on a dedicated STA thread** with its own `Dispatcher` loop — the framework UI thread is WinForms and breaks WPF keyboard input. `MenuEntry` handles this; never call `new MainWindow().Show()` from the framework UI thread. HandyControl themes need manual `Application.Resources` merging (no `App.xaml`).
- **Pending images bridge turns.** `AddPictureToContext` queues image hashes into `ChatContext.PendingImageHashes`; because `ChatContext` is ephemeral, `ChatCommands` persists them in a static per-group dict and re-injects on the next turn, where `DoChatAsync` injects them as native multimodal `ContentPart`s.
- **Reply dedup** runs before sending: `DoChatAsync` checks the candidate against the last 5 assistant messages via Levenshtein similarity (≥0.85 → skip). No extra LLM call.
- **AMN2 dispatches to only ONE handler per interface type.** `ChatCommands` is the sole `CommandHandlerBase`; explicit `[Command]`/`[DynamicCommand]` methods take priority over `OnNoMatchAsync` (which runs the pipeline).
- **All Core subsystems are static classes** — thread safety relies on the per-context pipeline gate and careful ordering. Adding mutable static state requires similar discipline. (`MCPExternalClient` and `ChatService` are notable exceptions — instances.)
- **The `Another-Mirai-Native.Abstractions` package is NOT on NuGet.org** — it comes from the AMN2 SDK. `ModelContextProtocol.AspNetCore` is the standard MCP SDK. Per `llms.txt`, pin exact NuGet versions (never floating) and prefer framework-bundled packages to keep the merged plugin DLL small.
- **File paths:** all plugin-generated files (config, DB, diary, schedules) MUST live under `API.AppApi.GetAppDirectory()`. Never write to CWD or `AppDomain.BaseDirectory`. Images sent via `MessageBuilder.Image()` must already exist under the framework's `data\image\` directory.
