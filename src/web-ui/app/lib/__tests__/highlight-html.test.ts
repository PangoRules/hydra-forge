import { describe, it, expect } from 'vitest'
import { highlightHtml } from '~/lib/highlight-html'

describe('highlightHtml', () => {
  it('wraps a single case-insensitive match in <mark>', () => {
    const result = highlightHtml('<p>Hello World</p>', 'world')
    expect(result).toContain('<mark')
    expect(result).toContain('World</mark>')
  })

  it('wraps multiple matches in the same text node', () => {
    const result = highlightHtml('<p>cat cat cat</p>', 'cat')
    expect(result.match(/<mark/g)?.length).toBe(3)
  })

  it('returns the original HTML unchanged when query is blank', () => {
    const result = highlightHtml('<p>Hello World</p>', '')
    expect(result).toBe('<p>Hello World</p>')
  })

  it('returns the original HTML unchanged when there is no match', () => {
    const result = highlightHtml('<p>Hello World</p>', 'xyz')
    expect(result).toBe('<p>Hello World</p>')
  })

  it('does not touch element attributes, only text content', () => {
    const result = highlightHtml('<a href="world.com">click</a>', 'world')
    expect(result).toContain('href="world.com"')
    expect(result).not.toContain('<mark')
  })
})
