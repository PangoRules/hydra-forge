import TurndownService from 'turndown'
import { gfm } from 'turndown-plugin-gfm'

// Single shared instance — used by MarkdownEditor.vue (Source-mode toggle, and its
// getMarkdown() expose) and directly by CardSpec/CardPlan for export, so a plan
// that's currently collapsed (its MarkdownEditor unmounted) can still be exported
// without needing to force it open first.
export const turndownService = new TurndownService({
  codeBlockStyle: 'fenced',
  headingStyle: 'atx'
})
// Tables, strikethrough (~~text~~) and GFM task lists (- [ ] item).
turndownService.use(gfm)

// Override <br> rule: use bare \n instead of two-trailing-spaces \n
// paired with marked breaks:true, bare \n round-trips cleanly (no whitespace-only lines)
turndownService.addRule('lineBreak', {
  filter: ['br'],
  replacement: () => '\n'
})

// Override listItem rule: strip <p> inside <li> extra newlines
// Tiptap wraps <li> content in <p>, Turndown paragraph rule adds \n\n...\n\n
// Default listItem keeps isParagraph trailing \n which gets indented → blank lines between items
turndownService.addRule('listItem', {
  filter: 'li',
  replacement: function (content, node, options) {
    const prefix0 = options.bulletListMarker + '   '
    const parent = node.parentNode as HTMLElement | null
    let prefix = prefix0
    if (parent?.nodeName === 'OL') {
      const start = parent.getAttribute('start')
      const index = Array.prototype.indexOf.call(parent.children, node)
      prefix = (start ? Number(start) + index : index + 1) + '.  '
    }
    content = content
      .replace(/^\n+/, '') // strip leading newlines
      .replace(/\n+$/, '') // strip trailing newlines (no isParagraph branch)
      .replace(/\n/g, '\n' + ' '.repeat(prefix.length))
    return prefix + content + (node.nextSibling ? '\n' : '')
  }
})

export function htmlToMarkdown(html: string): string {
  return turndownService.turndown(html)
}
