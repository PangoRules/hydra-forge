export function highlightHtml(html: string, query: string): string {
  const trimmed = query.trim()
  if (!import.meta.client || !trimmed) return html

  const container = document.createElement('div')
  container.innerHTML = html
  const needle = trimmed.toLowerCase()

  const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT)
  const textNodes: Text[] = []
  let node = walker.nextNode()
  while (node) {
    textNodes.push(node as Text)
    node = walker.nextNode()
  }

  for (const textNode of textNodes) {
    const text = textNode.textContent ?? ''
    const lower = text.toLowerCase()
    if (!lower.includes(needle)) continue

    const frag = document.createDocumentFragment()
    let cursor = 0
    let idx = lower.indexOf(needle, cursor)
    while (idx !== -1) {
      if (idx > cursor) frag.appendChild(document.createTextNode(text.slice(cursor, idx)))
      const mark = document.createElement('mark')
      mark.className = 'bg-yellow-300/60 dark:bg-yellow-500/40 rounded-sm px-0.5'
      mark.textContent = text.slice(idx, idx + needle.length)
      frag.appendChild(mark)
      cursor = idx + needle.length
      idx = lower.indexOf(needle, cursor)
    }
    if (cursor < text.length) frag.appendChild(document.createTextNode(text.slice(cursor)))
    textNode.replaceWith(frag)
  }

  return container.innerHTML
}
