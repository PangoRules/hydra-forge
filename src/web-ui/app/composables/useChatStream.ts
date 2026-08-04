import * as signalR from '@microsoft/signalr'
import { ApiRoutes } from '~/lib/routes'
import type { ChatMessageDto } from '~/types/chat'

const BACKOFF_MS = [5000, 10000, 30000, 60000]

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

  function clearStreaming() {
    streamingMessage.value = null
    isStreaming.value = false
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
    })

    conn.onreconnected(() => {
      isReconnecting.value = false
      backoffIndex = 0
      isConnected.value = true
      // Re-join session if we had one
      if (currentSessionId) {
        conn.invoke('JoinSession', currentSessionId).catch(() => {
          /* silent — will retry on next reconnect */
        })
      }
    })

    conn.onclose(() => {
      isConnected.value = false
      isReconnecting.value = false
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
        try {
          await connection.invoke('JoinSession', currentSessionId)
        } catch {
          /* silent — matches join()'s own failure handling */
        }
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
        connection.start().then(async () => {
          isConnected.value = true
          backoffIndex = 0
          if (currentSessionId) {
            await connection!.invoke('JoinSession', currentSessionId)
          }
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
    const connected = await waitForConnected()
    if (!connected || !connection) return
    try {
      await connection.invoke('JoinSession', sessionId)
    } catch {
      /* silent */
    }
  }

  async function leave() {
    if (!connection || !currentSessionId) return
    try {
      await connection.invoke('LeaveSession', currentSessionId)
    } catch {
      /* best effort */
    }
    currentSessionId = null
  }

  /**
   * Send a user message:
   * 1. POST to persist the user message — plain REST, doesn't need the hub,
   *    so the message is never lost even if the hub is slow/still connecting.
   * 2. Invoke SendMessage on the hub to trigger the streaming reply, waiting
   *    (bounded) for the connection if it isn't ready yet. If that wait or
   *    the invoke fails, the message is still saved — streamStarted:false
   *    tells the caller the reply didn't start so it can say so.
   */
  async function send(
    sessionId: string,
    content: string,
    presetId?: string,
    preferredProviderModelConfigId?: string
  ): Promise<{ userMessage: ChatMessageDto, streamStarted: boolean } | undefined> {
    if (sendingLock.value) return
    sendingLock.value = true
    try {
      const result = await api.POST<ChatMessageDto>(
        ApiRoutes.Chat.sessions.sendMessage(sessionId),
        { body: { content } }
      )
      if (!result.data) throw new Error('Failed to send message: no response')
      const userMessage = result.data

      const connected = await waitForConnected()
      if (!connected || !connection) {
        toast.error('Message saved, but the connection is too slow to start a reply — try again in a moment.')
        return { userMessage, streamStarted: false }
      }

      try {
        // presetId/preferredProviderModelConfigId as raw strings, not Guid wrappers
        await connection.invoke(
          'SendMessage',
          sessionId,
          userMessage.id,
          presetId ?? null,
          preferredProviderModelConfigId ?? null
        )
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
    preferredProviderModelConfigId?: string
  ) {
    if (sendingLock.value) return
    sendingLock.value = true
    try {
      const connected = await waitForConnected()
      if (!connected || !connection) {
        toast.error('Connection is too slow right now — try again in a moment.')
        return
      }
      await connection.invoke(
        'SendMessage',
        sessionId,
        userMessageId,
        presetId ?? null,
        preferredProviderModelConfigId ?? null
      )
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
    onStreamError: onStreamErrorCb
  }
}
