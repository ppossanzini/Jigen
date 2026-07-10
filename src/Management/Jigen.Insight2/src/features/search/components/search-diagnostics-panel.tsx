import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { SearchDiagnostics } from '../data/schema'

type SearchDiagnosticsPanelProps = {
  diagnostics: SearchDiagnostics
}

export function SearchDiagnosticsPanel({
  diagnostics,
}: SearchDiagnosticsPanelProps) {
  const items: { label: string; value: number }[] = [
    { label: 'Embedding', value: diagnostics.embeddingsCalculationTimeMs },
    { label: 'Search', value: diagnostics.searchTimeMs },
    { label: 'Merge', value: diagnostics.mergeTimeMs },
    { label: 'Sorting', value: diagnostics.sortingTimeMs },
    { label: 'Total', value: diagnostics.totalTimeMs },
  ]

  return (
    <div className='grid grid-cols-2 gap-3 sm:grid-cols-5'>
      {items.map(({ label, value }) => (
        <Card key={label} className='gap-1 py-3'>
          <CardHeader className='px-3'>
            <CardTitle className='text-muted-foreground text-xs font-normal'>
              {label}
            </CardTitle>
          </CardHeader>
          <CardContent className='px-3 text-lg font-semibold'>
            {value.toFixed(1)} ms
          </CardContent>
        </Card>
      ))}
    </div>
  )
}
