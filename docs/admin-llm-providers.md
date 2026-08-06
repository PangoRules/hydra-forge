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

## MiniMax (cloud, OpenAI-compatible)

MiniMax exposes an OpenAI-compatible API at `https://api.minimax.io/v1` — same wire shape as OpenRouter, so it rides the existing `OpenAiCompatible` adapter. Two key types work as the Bearer token, pick based on how you pay:

- **Subscription Key** — tied to a Token Plan subscription (Plus / Max / Ultra, yearly or monthly) or purchased Credits. Flat fee, quota-bounded (5-hour rolling + weekly windows). This is what a "yearly starter subscription" gives you.
- **API Key** (pay-as-you-go) — billed per token against your account balance. No quota windows; covers all modalities including video.

Both keys hit the same `/v1/chat/completions` endpoint with the same `Authorization: Bearer {key}` header. They are **not interchangeable for billing**: a Subscription Key has no pay-as-you-go balance, and an API Key has no Token Plan quota. A Subscription Key only becomes usable once an active Token Plan seat or Credits is assigned to the account — it can exist before that but will reject calls.

1. Get your key:
   - **Subscription Key**: MiniMax console → Account → Token Plan (`platform.minimax.io/user-center/payment/token-plan`). Copy the Subscription Key.
   - **API Key** (pay-as-you-go): MiniMax console → API Keys → Create new secret key (`platform.minimax.io/user-center/basic-information/interface-key`). Requires a balance top-up.
2. Providers → **Add Provider**:
   - `Name`: `MiniMax` (or whatever you want)
   - `Base URL`: `https://api.minimax.io/v1`
   - `Adapter Type`: `OpenAiCompatible`
   - `Provider Type`: `Text` — MiniMax the platform also serves image/speech/video/music, but HydraForge's `OpenAiCompatible` adapter only wires text chat + embeddings. Image generation has no MiniMax adapter (only `DallE`/`StabilityAi`/`ComfyUi`/`Diffusers` are wired for images), so setting `Both` here would let the admin route an image `AiFeature` to MiniMax and it would throw `NotSupportedException` at call time. Leave it `Text` unless/until a MiniMax image adapter is added.
   - `API Key`: your Subscription Key **or** pay-as-you-go API Key. Encrypted at rest (AES-256-GCM via `IKeyVault`) — never stored or logged in plaintext.
   - `Tier`: pick one.
3. Provider Models → select the MiniMax provider → **Discover Models** (hits `/v1/models`, lists the M-series). Add the ones you want:
   - `MiniMax-M3` — latest, 1M context, multimodal, coding/agentic SOTA. Recommended default.
   - `MiniMax-M2.7` / `MiniMax-M2.7-highspeed` — 204K context.
   - `MiniMax-M2.5` / `MiniMax-M2.5-highspeed`, `MiniMax-M2.1` / `MiniMax-M2.1-highspeed`, `MiniMax-M2` — legacy.
   - The `-highspeed` variants trade a little quality for ~2x throughput.
4. Routing → map `PersonalChat` (and `ProjectChat` if needed) → MiniMax provider → the tier you assigned.
5. Test at `/chats`.

### Token Plan quota caveats

- Token Plan uses **5-hour rolling + weekly quota windows**. Hit the limit and requests fail (`1002 Rate limit triggered` / `1008 Insufficient balance`) until the window resets. Unused quota does not roll over.
- When quota is exhausted your options are: wait for reset, buy Credits (same Subscription Key covers them), or swap the provider's API Key field to a pay-as-you-go key (separate key, needs balance).
- Token Plan covers the M-series text models + image/speech/music. It does **not** cover H3 video, voice design, or rapid voice cloning — irrelevant for HydraForge text chat, but worth knowing if you reuse the same key elsewhere.
- `MiniMax-M3` has **thinking on by default** — responses include inline reasoning mixed into the message text. This is normal, not a bug. M2.x models always think and cannot disable it.

## Z.ai (cloud, OpenAI-compatible)

Z.ai exposes an OpenAI-compatible API at `https://api.z.ai/api/paas/v4` — same wire shape as MiniMax/OpenRouter, rides the existing `OpenAiCompatible` adapter. Two key types, both Bearer:

- **GLM Coding Plan** — subscription (Lite $18/mo, Pro, Max), credit-based with 5-hour rolling + weekly windows. Credits map to GLM-5.2 / GLM-5-Turbo / GLM-4.7 tokens. Off-peak hours (UTC+8 14:00–18:00) charge 50% credit rate.
- **API Key** (pay-as-you-go) — billed per token against balance. GLM-5.2 = $1.40/M input, $4.40/M output. GLM-4.7 cheaper at $0.60/$2.20. Free tier exists: `glm-4.7-flash`, `glm-4.5-flash` (no sub needed).

Both keys hit the same `/chat/completions` endpoint with `Authorization: Bearer {key}`. Same pattern as MiniMax — not interchangeable for billing.

1. Get your key:
   - **Coding Plan**: Z.ai console → API Keys (`z.ai/manage-apikey/apikey-list`). Subscribe to a Coding Plan first.
   - **Pay-as-you-go**: same API Keys page, needs balance top-up.
2. Providers → **Add Provider**:
   - `Name`: `Z.ai` (or whatever you want)
   - `Base URL`: `https://api.z.ai/api/paas/v4`
   - `Adapter Type`: `OpenAiCompatible`
   - `Provider Type`: `Text` — Z.ai the platform also serves image/video/audio, but HydraForge has no Z.ai image adapter. Same caveat as MiniMax.
   - `API Key`: your Coding Plan key **or** pay-as-you-go API Key. Encrypted at rest (AES-256-GCM via `IKeyVault`) — never stored or logged in plaintext.
   - `Tier`: pick one.
3. Provider Models → select the Z.ai provider → **Discover Models** (hits `/models`). Add the ones you want:
   - `glm-5.2` — flagship, coding SOTA. Recommended default.
   - `glm-5-turbo` — faster, cheaper.
   - `glm-4.7`, `glm-4.6` — cheaper still.
   - `glm-4.7-flash`, `glm-4.5-flash` — free tier, no sub needed.
4. Routing → map `PersonalChat` (and `ProjectChat` if needed) → Z.ai provider → the tier you assigned.
5. Test at `/chats`.

### Coding Plan quota caveats

- 5-hour rolling + weekly credit windows. Lite = 2,000 credits/5hr, 10,000/week. Pro = 12,000/60,000. Max = 28,000/140,000.
- Credits = (input tokens × input multiplier + output tokens × output multiplier) / 10,000. GLM-5.2 multipliers: input 6.9, cached input 1.7, output 24.
- Off-peak = 50% credit cost. Peak = Mon–Fri 14:00–18:00 UTC+8.
- Quota exhausted → wait for reset, or swap to pay-as-you-go API Key (separate key, needs balance).
- GLM-5.2 supports thinking mode (`thinking.type: enabled`) — inline reasoning in response. Not sent by HydraForge's adapter (only sends `reasoning_effort` when set on the request), so thinking stays off unless you wire it yourself.

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
