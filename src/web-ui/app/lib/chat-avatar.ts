/**
 * Model name → brand icon, via the `simple-icons` iconify collection (already bundled,
 * works offline). Keyed by substring match against the lowercased model name since
 * admins can point any AdapterType at arbitrary model IDs (OpenRouter's "deepseek/...",
 * a raw Ollama pull like "llama3.1", etc.) — there's no fixed enum of "the models" to
 * switch on, just brand keywords that tend to show up in how they're named.
 * Order matters: more specific keywords first so e.g. "claude" wins over a later, looser
 * match for the same family.
 */
const MODEL_ICON_KEYWORDS: [string, string][] = [
  ['claude', 'simple-icons:claude'],
  ['anthropic', 'simple-icons:anthropic'],
  ['gpt', 'simple-icons:openai'],
  ['openai', 'simple-icons:openai'],
  ['deepseek', 'simple-icons:deepseek'],
  ['qwen', 'simple-icons:qwen'],
  ['llama', 'simple-icons:meta'],
  ['gemini', 'simple-icons:googlegemini'],
  ['gemma', 'simple-icons:google'],
  ['mistral', 'simple-icons:mistralai'],
  ['mixtral', 'simple-icons:mistralai'],
  ['perplexity', 'simple-icons:perplexity'],
  ['nvidia', 'simple-icons:nvidia']
]

/** Iconify icon name for a model, or null if no known brand match — caller should fall back to initials. */
export function getModelIcon(modelName: string | null | undefined): string | null {
  if (!modelName) return null
  const lower = modelName.toLowerCase()
  for (const [keyword, icon] of MODEL_ICON_KEYWORDS) {
    if (lower.includes(keyword)) return icon
  }
  return null
}

/** 1-2 letter initials from a name, e.g. "DeepSeek: DeepSeek V4 Flash" → "DD", "qwen3-coder" → "QW". */
export function getInitials(name: string | null | undefined, fallback = '?'): string {
  const source = name?.trim()
  if (!source) return fallback.slice(0, 2).toUpperCase()
  const words = source.split(/[\s:/_-]+/).filter(Boolean)
  if (words.length === 1) return words[0]!.slice(0, 2).toUpperCase()
  return (words[0]![0]! + words[1]![0]!).toUpperCase()
}
