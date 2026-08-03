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
  const { getToken } = useAuthToken()
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
    const token = getToken()
    if (!token) throw new Error('No auth token')

    const hubUrl = `${config.public.signalrBaseUrl}/hubs/chat`
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
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

    const token = getToken()
    if (!token) return

    connection = buildConnection()

    try {
      await connection.start()
      isConnected.value = true
      backoffIndex = 0
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

  async function join(sessionId: string) {
    if (!connection) return
    currentSessionId = sessionId
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
   * 1. POST to persist the user message → get messageId
   * 2. Invoke SendMessage on the hub to trigger streaming response
   */
  async function send(sessionId: string, content: string, presetId?: string) {
    if (sendingLock.value) return
    if (!connection) throw new Error('Not connected')
    sendingLock.value = true
    try {
      // Step 1: persist user message
      const result = await api.POST<ChatMessageDto>(
        ApiRoutes.Chat.sessions.sendMessage(sessionId),
        { body: { content } }
      )
      if (!result.data) throw new Error('Failed to send message: no response')
      const data = result.data

      // Step 2: invoke streaming — pass presetId as raw string, not Guid wrapper
      await connection.invoke(
        'SendMessage',
        sessionId,
        data.id,
        presetId ?? null
      )
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
