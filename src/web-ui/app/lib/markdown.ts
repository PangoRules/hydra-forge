import { marked, Renderer } from 'marked'
import DOMPurify from 'dompurify'

// Some reasoning models inline raw <think>...</think> reasoning in the content field
// itself (Ollama instead puts it in a separate message.thinking field, handled
// server-side via think:false — see OllamaAdapter). marked's html-stripping renderer
// below only removes the literal <think>/</think> tag markers as isolated inline HTML
// tokens — it does NOT swallow the text between them, so the reasoning trace itself
// would still render as plain paragraph text. Strip the whole block (tags + content)
// before handing off to marked. Case-insensitive, spans newlines, non-greedy so
// multiple distinct blocks don't merge into one match.
const THINK_BLOCK_PATTERN = /<think>[\s\S]*?<\/think>/gi

/**
 * Shared markdown renderer for both persisted messages (ChatMessageBubble) and the
 * live-streaming bubble (ChatMessageList) — previously the streaming bubble interpolated
 * raw text with no parsing at all, so markdown syntax was visible as literal characters
 * while streaming, then "snapped" to formatted only once the finished message re-rendered
 * through this same parser. Using it in both places from the start removes that flash
 * instead of just hiding it at the end.
 */
export function renderMarkdown(content: string): string {
  if (!content) return ''
  const withoutThinking = content.replace(THINK_BLOCK_PATTERN, '')
  if (!withoutThinking) return ''
  // Strip any other raw HTML by providing a no-op html renderer.
  const renderer = new Renderer()
  renderer.html = () => ''
  const html = marked.parse(withoutThinking, { async: false, breaks: true, renderer }) as string
  // DOMPurify needs a real DOM — no-op on the server, the no-op html renderer above is
  // what keeps SSR output safe; the client re-render is the actual XSS defense.
  if (!import.meta.client) return html
  return DOMPurify.sanitize(html)
}
