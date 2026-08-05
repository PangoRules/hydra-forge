export type DocumentExportFormat = 'md'

function slugify(title: string): string {
  const slug = title
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
  return slug || 'untitled'
}

interface ExportPayload {
  blob: Blob
  filename: string
}

// One entry per format — adding PDF/HTML export later is a new case here,
// not a rewrite of the download mechanics below.
const exporters: Record<DocumentExportFormat, (title: string, markdown: string) => ExportPayload> = {
  md: (title, markdown) => ({
    blob: new Blob([markdown], { type: 'text/markdown;charset=utf-8' }),
    filename: `${slugify(title)}.md`
  })
}

/**
 * Downloads a Spec/Plan document client-side. Markdown is the only format for
 * now — Content is already Markdown at rest, no server round-trip needed.
 */
export function useDocumentExport() {
  function exportDocument(title: string, markdown: string, format: DocumentExportFormat = 'md') {
    const { blob, filename } = exporters[format](title, markdown)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
  }

  return { exportDocument }
}
