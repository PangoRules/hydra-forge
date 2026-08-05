# Admin: Registering LLM Providers

How to wire an LLM backend into HydraForge so chat (and other AI features) have a model to route to. Admin-only — regular users cannot add personal API keys (`docs/scope.md`).

Every provider goes through the same three admin pages, in order:

1. **Providers** (`/admin/providers`) — register the backend (base URL, adapter type, API key).
2. **Provider Models** (`/admin/provider-models`) — pick which models on that provider are usable, with a `Tier` and `MaxTokens`. Use **Discover Models** to pull the live list from the provider instead of typing model IDs by hand.
3. **Routing** (`/admin/routing`) — map an `AiFeature` (e.g. `PersonalChat`, `ProjectChat`) to a provider/tier. A model isn't reachable by any feature until it's routed here.

A provider with no routed model is inert — chat will fail with a routing error (`StreamError` in the UI) until step 3 is done.

## OpenRouter (cloud, OpenAI-compatible)

1. Providers → **Add Provider**:
   - `Name`: `OpenRouter` (or whatever you want to call it)
   - `Base URL`: `https://openrouter.ai/api/v1`
   - `Adapter Type`: `OpenAiCompatible`
   - `Provider Type`: `Text`
   - `API Key`: your OpenRouter key (get one at openrouter.ai/keys). Encrypted at rest (AES-256-GCM via `IKeyVault`) — never stored or logged in plaintext.
   - `Tier`: pick a default (`Economy`/`Standard`/`Premium`) — this is the provider's fallback tier, individual models can override it.
2. Provider Models → select the OpenRouter provider → **Discover Models**. OpenRouter lists hundreds of models — use the filter box in the modal (searches name + model ID) to find the ones you want, then **Add** each one. This opens the Add Model form pre-filled with that model's ID/name; confirm `Tier`/`MaxTokens` and save.
3. Routing → set `PersonalChat` (and `ProjectChat` if you want project-scoped chat too) → the OpenRouter provider → the tier you assigned above.
4. Test at `/chats`.

## Ollama (local)

1. Make sure Ollama is running and has at least one model pulled: `ollama pull llama3.1` (or whatever you use).
2. Providers → **Add Provider**:
   - `Name`: `Ollama` (or whatever you want to call it)
   - `Base URL`: see below — depends on how the HydraForge server itself is running
   - `Adapter Type`: `Ollama`
   - `Provider Type`: `Text`
   - `API Key`: leave blank — Ollama has no auth
   - `Tier`: pick one
3. Provider Models → select the Ollama provider → **Discover Models** (hits Ollama's `/api/tags`, lists whatever you've pulled locally) → filter/Add.
4. Routing → map a feature to it, same as OpenRouter above.

### Base URL — bare-metal vs Docker

- **Server run via `dotnet run --project src/HydraForge.Server`** (bare metal, same host as Ollama): `http://localhost:11434` — the default Ollama listens on.
- **Server run via `docker compose up`** (containerized): `localhost` inside the container is the container itself, not your host, so this needs both sides adjusted:
  - Ollama must listen on all interfaces, not just loopback: run it with `OLLAMA_HOST=0.0.0.0:11434 ollama serve` (or set that env var wherever your Ollama service/systemd unit is configured).
  - The `server` service in `docker-compose.yml` has no `host.docker.internal` mapping yet. Either add `extra_hosts: ["host.docker.internal:host-gateway"]` under the `server` service and use `http://host.docker.internal:11434` as the Base URL, or use the Docker bridge gateway IP directly (`ip route | grep default` from inside the container, typically `172.17.0.1`).

## Notes

- `Discover Models` calls each adapter's `GetModelsAsync` — supported by `OpenAiCompatible` and `Ollama` today. Adapters that don't implement it just return an empty/error result; use the manual **Add Model** button on the Provider Models page instead (type the model ID in yourself).
- Registering a provider does not consume budget/quota by itself — usage is only recorded on actual chat calls (`IUsageRecorder`, see `CLAUDE.md` LLM section).
