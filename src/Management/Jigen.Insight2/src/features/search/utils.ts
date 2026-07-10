import { toDisplayKey } from '@/lib/base64'
import type { CollectionSearchResultItem } from '@/lib/api-types'
import type { SearchResultRow } from './data/schema'

function toDisplayContent(content: unknown): string {
  if (content === null || content === undefined) return ''
  try {
    return JSON.stringify(content)
  } catch {
    return String(content)
  }
}

/** Maps raw search result items (merged or per-collection) into display-ready rows, sorted by score desc. */
export function toResultRows(
  items: CollectionSearchResultItem[] | null | undefined,
  fallbackSearchTimeMs: number,
  fallbackCollection = ''
): SearchResultRow[] {
  const source = items ?? []

  return source
    .map((item, index): SearchResultRow => {
      const collection = fallbackCollection || 'unknown'
      const rawScore = Number(item.score ?? 0)
      const score = Number.isFinite(rawScore) ? Number(rawScore.toFixed(4)) : 0
      const key = item.key ? toDisplayKey(item.key) : ''

      return {
        id: key || `${collection}-${index + 1}`,
        collection,
        key,
        content: toDisplayContent(item.content),
        score,
        searchTimeMs: fallbackSearchTimeMs,
      }
    })
    .sort((left, right) => right.score - left.score)
}
