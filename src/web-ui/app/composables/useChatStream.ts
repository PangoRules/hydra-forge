import * as signalR from '@microsoft/signalr'
import { ApiRoutes } from '~/lib/routes'
import type { ChatMessageDto } from '~/types/chat'

const BACKOFF_MS = [5000, 10000, 30000, 60000]

// Bounds the persist/trigger REST calls in send()/resend() so a stuck request (observed:
// requests can sit pending with no error at all if a slow-to-negotiate SignalR handshake is
// hogging the browser's small per-origin HTTP/1.1 connection pool) surfaces a toast instead
// of silently hanging forever with zero feedback.
const REQUEST_TIMEOUT_MS = 20000

export interface StreamingMessage {
  messageId: string
  modelId: string
  modelName: string
  content: string
}

export function useChatStream() {
  const authStore = useAuthStore()
  const config = useRuntimeConfig()
  const toast = useAppToast()
  const api = useApi()

  let connection: signalR.HubConnection | null = null
  let currentSessionId: string | null = null
  let backoffIndex = 0
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null

  // Group membership is per-ConnectionId, not per-user — every reconnect (and
  // there's no shortage of those on mobile) gets a brand new ConnectionId and
  // loses it entirely. `send()`/`resend()` invoking SendMessage on a
  // connection that's technically Connected but hasn't actually finished
  // joining the session's group yet is the failure mode that matters most:
  // the invoke succeeds (no error, no toast — invoking a hub method doesn't
  // require group membership), the server generates and persists the whole
  // reply, and broadcasts it to a group this connection was never added to.
  // Nothing ever reaches the client and nothing ever reports failure. Slower
  // connect+join round trips over some Tailscale/mobile paths (observed
  // 9-67s) widen this race far beyond what desktop ever sees. Tracking the
  // joined session id + a shared in-flight promise lets every caller that
  // needs group membership (join/send/resend/reconnect handlers) await the
  // *actual join*, not just the raw connection state.
  let joinedSessionId: string | null = null
  let joinPromise: Promise<void> | null = null

  // Reactive streaming state
  const streamingMessage = ref<StreamingMessage | null>(null)
  const isStreaming = ref(false)
  const isConnected = ref(false)
  const isReconnecting = ref(false)
  const sendingLock = ref(false)

  // Callbacks set by the consumer
  let onStreamStart: ((messageId: string, modelId: string, modelName: string) => void) | null = null
  let onStreamDelta: ((messageId: string, delta: string) => void) | null = null
  let onStreamDone:
    | ((
      messageId: string,
      inputTokens: number | null,
      outputTokens: number | null,
      cachedTokens: number | null,
      modelName: string | null
    ) => void)
    | null = null
  let onStreamError: ((messageId: string, code: string, message: string) => void) | null = null
  let onSessionUpdated: ((sessionId: string, title: string, status: string) => void) | null = null
  let onReconnectedHandler: (() => void) | null = null

  function onStreamStartCb(
    cb: (messageId: string, modelId: string, modelName: string) => void
  ) {
    onStreamStart = cb
  }

  function onStreamDeltaCb(cb: (messageId: string, delta: string) => void) {
    onStreamDelta = cb
  }

  function onStreamDoneCb(
    cb: (
      messageId: string,
      inputTokens: number | null,
      outputTokens: number | null,
      cachedTokens: number | null,
      modelName: string | null
    ) => void
  ) {
    onStreamDone = cb
  }

  function onStreamErrorCb(
    cb: (messageId: string, code: string, message: string) => void
  ) {
    onStreamError = cb
  }

  // Pushed when a session's title (or other server-set fields) changes outside the
  // caller's own request cycle — e.g. title generation, which runs as its own decoupled
  // Hangfire job specifically so it doesn't block the chat reply's own completion. Without
  // this, a connected client had no way to learn the title changed short of a refresh.
  function onSessionUpdatedCb(
    cb: (sessionId: string, title: string, status: string) => void
  ) {
    onSessionUpdated = cb
  }

  // Fired after every reconnect (including the automatic ones SignalR retries
  // internally) — mobile networks reconnect far more often mid-stream than
  // desktop, and a reconnect that lands *after* the server already broadcast
  // StreamDone to the (now-stale) group membership means that event is gone
  // for good, no replay. The consumer uses this to re-fetch the session over
  // REST and pick up anything that finished while disconnected.
  function onReconnectedCb(cb: () => void) {
    onReconnectedHandler = cb
  }

  function clearStreaming() {
    streamingMessage.value = null
    isStreaming.value = false
  }

  // Idempotent, shared across concurrent callers: two calls for the same
  // session while a join is already in flight await the same promise instead
  // of firing duplicate JoinSession invokes (harmless server-side, but
  // pointless). Returns false if the connection never came up or the invoke
  // itself failed — callers treat that as "not joined, don't expect a reply."
  async function ensureJoined(sessionId: string): Promise<boolean> {
    const connected = await waitForConnected()
    if (!connected || !connection) return false
    if (joinedSessionId === sessionId) return true

    if (!joinPromise) {
      const conn = connection
      joinPromise = conn
        .invoke('JoinSession', sessionId)
        .then(() => {
          joinedSessionId = sessionId
        })
        .catch(() => {
          /* leave joinedSessionId unset — caller sees ensureJoined() return false */
        })
        .finally(() => {
          joinPromise = null
        })
    }
    await joinPromise
    return joinedSessionId === sessionId
  }

  function buildConnection(): signalR.HubConnection {
    const token = authStore.token
    if (!token) throw new Error('No auth token')

    const hubUrl = `${config.public.signalrBaseUrl}/hubs/chat`
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.onreconnecting(() => {
      isReconnecting.value = true
      // New ConnectionId is coming — the old group membership is already gone.
      joinedSessionId = null
    })

    conn.onreconnected(() => {
      isReconnecting.value = false
      backoffIndex = 0
      isConnected.value = true
      if (currentSessionId) {
        void ensureJoined(currentSessionId)
      }
      onReconnectedHandler?.()
    })

    conn.onclose(() => {
      isConnected.value = false
      isReconnecting.value = false
      joinedSessionId = null
      scheduleReconnect()
    })

    conn.on('StreamStart', (messageId: string, modelId: string, modelName: string) => {
      streamingMessage.value = { messageId, modelId, modelName, content: '' }
      isStreaming.value = true
      onStreamStart?.(messageId, modelId, modelName)
    })

    conn.on('StreamDelta', (messageId: string, delta: string) => {
      if (streamingMessage.value?.messageId === messageId) {
        streamingMessage.value.content += delta
      }
      onStreamDelta?.(messageId, delta)
    })

    conn.on(
      'StreamDone',
      (
        messageId: string,
        inputTokens: number | null,
        outputTokens: number | null,
        cachedTokens: number | null,
        modelName: string | null
      ) => {
        if (streamingMessage.value?.messageId === messageId) {
          clearStreaming()
        }
        onStreamDone?.(messageId, inputTokens, outputTokens, cachedTokens, modelName)
      }
    )

    conn.on('StreamError', (messageId: string, code: string, message: string) => {
      if (streamingMessage.value?.messageId === messageId) {
        clearStreaming()
      }
      toast.error(`Chat error: ${message}`)
      onStreamError?.(messageId, code, message)
    })

    conn.on('SessionUpdated', (sessionId: string, title: string, status: string) => {
      onSessionUpdated?.(sessionId, title, status)
    })

    return conn
  }

  async function connect() {
    if (connection) return

    if (!authStore.token) return

    connection = buildConnection()

    try {
      await connection.start()
      isConnected.value = true
      backoffIndex = 0
      // join()'s own wait may already have given up by the time a slow
      // initial handshake finally lands — catch up here, same as
      // onreconnected() does for a connection that drops and comes back.
      if (currentSessionId) {
        void ensureJoined(currentSessionId)
      }
    } catch {
      scheduleReconnect()
    }
  }

  function scheduleReconnect() {
    if (reconnectTimer) return
    const delay = BACKOFF_MS[Math.min(backoffIndex, BACKOFF_MS.length - 1)]
    backoffIndex++
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null
      if (connection?.state === signalR.HubConnectionState.Disconnected) {
        connection.start().then(() => {
          isConnected.value = true
          backoffIndex = 0
          if (currentSessionId) {
            void ensureJoined(currentSessionId)
          }
          onReconnectedHandler?.()
        }).catch(() => {
          scheduleReconnect()
        })
      }
    }, delay)
  }

  async function disconnect() {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer)
      reconnectTimer = null
    }
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        if (currentSessionId) {
          await connection.invoke('LeaveSession', currentSessionId)
        }
        await connection.stop()
      } catch {
        /* best effort */
      }
    }
    connection = null
    currentSessionId = null
    joinedSessionId = null
    joinPromise = null
    isConnected.value = false
    isReconnecting.value = false
    clearStreaming()
  }

  // Polls connection.state rather than awaiting connect()'s own promise —
  // callers (join/send/resend) may run before, during, or well after the
  // initial connect() attempt, including through slow/retrying handshakes
  // (seen in practice: 60s+ over some Tailscale paths). Bounded so a stalled
  // connection surfaces a clear error instead of hanging the caller forever.
  // 90s default: observed real handshake times of 9-67s over some Tailscale
  // paths (server-measured, not a guess) — a shorter bound would give up on
  // triggering the reply before a legitimately-slow-but-working connection
  // ever lands.
  function waitForConnected(timeoutMs = 90000): Promise<boolean> {
    return new Promise((resolve) => {
      const start = Date.now()
      const check = () => {
        if (connection?.state === signalR.HubConnectionState.Connected) {
          resolve(true)
          return
        }
        if (Date.now() - start >= timeoutMs) {
          resolve(false)
          return
        }
        setTimeout(check, 250)
      }
      check()
    })
  }

  async function join(sessionId: string) {
    currentSessionId = sessionId
    await ensureJoined(sessionId)
  }

  async function leave() {
    if (!connection || !currentSessionId) return
    try {
      await connection.invoke('LeaveSession', currentSessionId)
    } catch {
      /* best effort */
    }
    if (joinedSessionId === currentSessionId) joinedSessionId = null
    currentSessionId = null
  }

  /**
   * Send a user message:
   * 1. POST to persist the user message.
   * 2. POST to trigger the reply — plain REST, deliberately not routed through the hub.
   *    Generation runs as a Hangfire job decoupled from this request's connection, so
   *    triggering it doesn't need (or wait for) a live SignalR connection at all — see
   *    ChatMessagesController.GenerateReply's doc comment for why that distinction matters
   *    on networks where the WebSocket handshake itself can take 90-170s+. SignalR is used
   *    only to *receive* the live stream if/when it happens to be connected; the
   *    stall-recovery poll in ChatSessionView.vue picks up the result over REST either way.
   */
  async function send(
    sessionId: string,
    content: string,
    presetId?: string,
    preferredProviderModelConfigId?: string,
    reasoningEffort?: string | null
  ): Promise<{ userMessage: ChatMessageDto, streamStarted: boolean } | undefined> {
    if (sendingLock.value) return
    sendingLock.value = true
    try {
      const result = await api.POST<ChatMessageDto>(
        ApiRoutes.Chat.sessions.sendMessage(sessionId),
        { body: { content }, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) }
      )
      if (!result.data) throw new Error('Failed to send message: no response')
      const userMessage = result.data

      try {
        await api.POST(ApiRoutes.Chat.sessions.generateReply(sessionId, userMessage.id), {
          body: {
            presetId: presetId ?? null,
            preferredProviderModelConfigId: preferredProviderModelConfigId ?? null,
            reasoningEffort: reasoningEffort ?? null
          },
          signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
        })
        return { userMessage, streamStarted: true }
      } catch {
        toast.error('Message saved, but the AI reply could not start — try again from the message.')
        return { userMessage, streamStarted: false }
      }
    } finally {
      sendingLock.value = false
    }
  }

  /**
   * Re-invoke streaming for an already-persisted user message — used by
   * "regenerate" after a rollback truncates the assistant reply that followed it.
   */
  async function resend(
    sessionId: string,
    userMessageId: string,
    presetId?: string,
    preferredProviderModelConfigId?: string,
    reasoningEffort?: string | null
  ) {
    if (sendingLock.value) return
    sendingLock.value = true
    try {
      await api.POST(ApiRoutes.Chat.sessions.generateReply(sessionId, userMessageId), {
        body: {
          presetId: presetId ?? null,
          preferredProviderModelConfigId: preferredProviderModelConfigId ?? null,
          reasoningEffort: reasoningEffort ?? null
        },
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
      })
    } catch {
      toast.error('Could not start the reply — try again.')
    } finally {
      sendingLock.value = false
    }
  }

  async function cancel(sessionId: string) {
    if (!connection) return
    try {
      await connection.invoke('CancelStream', sessionId)
    } catch {
      /* best effort */
    }
    clearStreaming()
    sendingLock.value = false
  }

  return {
    connect,
    disconnect,
    join,
    leave,
    send,
    resend,
    cancel,
    streamingMessage,
    isStreaming,
    isConnected,
    isReconnecting,
    onStreamStart: onStreamStartCb,
    onStreamDelta: onStreamDeltaCb,
    onStreamDone: onStreamDoneCb,
    onStreamError: onStreamErrorCb,
    onSessionUpdated: onSessionUpdatedCb,
    onReconnected: onReconnectedCb,
    // Force-clears a stuck "typing…" bubble when the consumer independently
    // confirms (via REST) that the reply already completed and persisted —
    // used by the stall-recovery poll, see ChatSessionView.vue.
    clearStreaming
  }
}
