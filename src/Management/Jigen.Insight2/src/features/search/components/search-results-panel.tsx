import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import type { SearchResultRow } from '../data/schema'

type SearchResultsPanelProps = {
  hasSearched: boolean
  mergedRows: SearchResultRow[]
  collectionsResults: { collection: string; rows: SearchResultRow[] }[]
}

export function SearchResultsPanel({
  hasSearched,
  mergedRows,
  collectionsResults,
}: SearchResultsPanelProps) {
  if (!hasSearched) {
    return (
      <p className='text-muted-foreground text-sm'>
        Run a search to see results here.
      </p>
    )
  }

  return (
    <Tabs defaultValue='merged'>
      <TabsList>
        <TabsTrigger value='merged'>Merged ({mergedRows.length})</TabsTrigger>
        {collectionsResults.map(({ collection, rows }) => (
          <TabsTrigger key={collection} value={collection}>
            {collection} ({rows.length})
          </TabsTrigger>
        ))}
      </TabsList>
      <TabsContent value='merged'>
        <ResultsTable rows={mergedRows} showCollection />
      </TabsContent>
      {collectionsResults.map(({ collection, rows }) => (
        <TabsContent key={collection} value={collection}>
          <ResultsTable rows={rows} showCollection={false} />
        </TabsContent>
      ))}
    </Tabs>
  )
}

function ResultsTable({
  rows,
  showCollection,
}: {
  rows: SearchResultRow[]
  showCollection: boolean
}) {
  if (rows.length === 0) {
    return (
      <p className='text-muted-foreground py-6 text-center text-sm'>
        No results.
      </p>
    )
  }

  return (
    <div className='overflow-hidden rounded-md border'>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Key</TableHead>
            {showCollection && <TableHead>Collection</TableHead>}
            <TableHead>Content</TableHead>
            <TableHead className='text-end'>Score</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={`${row.collection}-${row.id}`}>
              <TableCell className='max-w-40 truncate font-mono text-xs'>
                {row.key || '—'}
              </TableCell>
              {showCollection && <TableCell>{row.collection}</TableCell>}
              <TableCell className='max-w-96 truncate font-mono text-xs'>
                {row.content || '—'}
              </TableCell>
              <TableCell className='text-end tabular-nums'>
                {row.score}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
