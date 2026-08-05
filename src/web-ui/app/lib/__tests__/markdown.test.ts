import { describe, it, expect } from 'vitest'
import { renderMarkdown } from '~/lib/markdown'

describe('renderMarkdown', () => {
  it('returns empty string for empty content', () => {
    expect(renderMarkdown('')).toBe('')
  })

  it('renders bold markdown syntax as HTML, not literal asterisks', () => {
    const html = renderMarkdown('**bold text**')
    expect(html).toContain('<strong>bold text</strong>')
    expect(html).not.toContain('**')
  })

  it('strips a <think>...</think> block entirely, tags and reasoning content', () => {
    // Some reasoning models emit raw <think>...</think> inline in content rather
    // than a separate field (Ollama does the latter, handled server-side via
    // think:false — this covers the client-visible case for models that don't).
    // marked's own html-stripping renderer only removes the tag markers, not the
    // text between them, so this has to happen before content reaches marked.
    const html = renderMarkdown('<think>internal reasoning about the request</think>The actual answer')
    expect(html).not.toContain('think')
    expect(html).not.toContain('internal reasoning')
    expect(html).toContain('The actual answer')
  })

  it('strips a multi-line <think> block spanning newlines', () => {
    const html = renderMarkdown('<think>\nstep one\nstep two\n</think>\n\nFinal answer here')
    expect(html).not.toContain('step one')
    expect(html).not.toContain('step two')
    expect(html).toContain('Final answer here')
  })

  it('strips multiple distinct <think> blocks without merging them', () => {
    const html = renderMarkdown('<think>first</think>Answer A<think>second</think>Answer B')
    expect(html).not.toContain('first')
    expect(html).not.toContain('second')
    expect(html).toContain('Answer A')
    expect(html).toContain('Answer B')
  })

  it('is a no-op when there is no think block', () => {
    const html = renderMarkdown('Just a plain answer')
    expect(html).toContain('Just a plain answer')
  })
})
