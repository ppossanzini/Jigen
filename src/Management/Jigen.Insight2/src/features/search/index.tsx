import { useState } from 'react'
import { getRouteApi } from '@tanstack/react-router'
import { ListFilter, Loader2, SearchIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { ThemeSwitch } from '@/components/theme-switch'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useCollections } from '@/features/collections/hooks'
import { useDatabases } from '@/features/databases/hooks'
import {
  useCalculateEmbeddings,
  useCalculateEmbeddingsWithTask,
  useEmbeddingTasks,
  useSearchCollections,
} from './hooks'
import { SearchCollectionsSelect } from './components/search-collections-select'
import { SearchFilterPanel } from './components/search-filter-panel'
import { SearchDiagnosticsPanel } from './components/search-diagnostics-panel'
import { SearchResultsPanel } from './components/search-results-panel'
import type { SearchDiagnostics, SearchResultRow } from './data/schema'
import { toResultRows } from './utils'

const route = getRouteApi('/_authenticated/search/')

export function Search() {
  const { db } = route.useSearch()
  const navigate = route.useNavigate()

  const [dbname, setDbname] = useState<string | null>(db ?? null)
  const [selectedCollections, setSelectedCollections] = useState<string[]>([])
  const [task, setTask] = useState<string | null>(null)
  const [sentence, setSentence] = useState('')
  const [top, setTop] = useState(10)

  const [hasSearched, setHasSearched] = useState(false)
  const [mergedRows, setMergedRows] = useState<SearchResultRow[]>([])
  const [collectionsResults, setCollectionsResults] = useState<
    { collection: string; rows: SearchResultRow[] }[]
  >([])
  const [diagnostics, setDiagnostics] = useState<SearchDiagnostics | null>(null)

  const { data: databases } = useDatabases()
  const { data: collections, isLoading: collectionsLoading } =
    useCollections(dbname)
  const { data: tasks } = useEmbeddingTasks()

  const calculateEmbeddings = useCalculateEmbeddings()
  const calculateEmbeddingsWithTask = useCalculateEmbeddingsWithTask()
  const searchCollections = useSearchCollections()

  const isSearching =
    calculateEmbeddings.isPending ||
    calculateEmbeddingsWithTask.isPending ||
    searchCollections.isPending

  const canSubmit =
    !!dbname && selectedCollections.length > 0 && sentence.trim().length > 0

  function handleDbnameChange(next: string) {
    setDbname(next)
    setSelectedCollections([])
    navigate({ search: (prev) => ({ ...prev, db: next }), replace: true })
  }

  function handleToggleCollection(collection: string) {
    setSelectedCollections((prev) =>
      prev.includes(collection)
        ? prev.filter((c) => c !== collection)
        : [...prev, collection]
    )
  }

  async function handleSearch() {
    if (!canSubmit || !dbname) return
    setHasSearched(true)

    const embeddingStart = performance.now()
    try {
      const embeddings = task
        ? await calculateEmbeddingsWithTask.mutateAsync({ task, text: sentence })
        : await calculateEmbeddings.mutateAsync(sentence)
      const embeddingsCalculationTimeMs = performance.now() - embeddingStart

      const result = await searchCollections.mutateAsync({
        dbname,
        data: {
          collections: selectedCollections,
          sentence,
          embeddings,
          top,
        },
      })

      const merged = toResultRows(
        result.mergedResults,
        Number(result.searchTime ?? 0)
      )
      const perCollection = (result.collectionsResults ?? []).map((entry) => ({
        collection: entry.collection ?? 'unknown',
        rows: toResultRows(
          entry.results,
          Number(entry.searchTime ?? 0),
          entry.collection ?? ''
        ),
      }))

      setMergedRows(
        merged.length > 0 ? merged : perCollection.flatMap((c) => c.rows)
      )
      setCollectionsResults(perCollection)

      const searchTimeMs = Number(result.searchTime ?? 0)
      const mergeTimeMs = Number(result.mergeTime ?? 0)
      const sortingTimeMs = Number(result.sortingTime ?? 0)

      setDiagnostics({
        embeddingsCalculationTimeMs,
        searchTimeMs,
        mergeTimeMs,
        sortingTimeMs,
        totalTimeMs:
          embeddingsCalculationTimeMs + searchTimeMs + mergeTimeMs + sortingTimeMs,
      })
    } catch {
      toast.error('Search failed.')
    }
  }

  return (
    <>
      <Header fixed>
        <Select value={dbname ?? undefined} onValueChange={handleDbnameChange}>
          <SelectTrigger className='h-8 w-32 sm:w-44'>
            <SelectValue placeholder='Database' />
          </SelectTrigger>
          <SelectContent>
            {(databases ?? []).map((name) => (
              <SelectItem key={name} value={name}>
                {name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <SearchCollectionsSelect
          dbname={dbname}
          collections={collections ?? []}
          collectionsLoading={collectionsLoading}
          selectedCollections={selectedCollections}
          onToggleCollection={handleToggleCollection}
        />
        <Input
          value={sentence}
          onChange={(event) => setSentence(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && canSubmit) {
              event.preventDefault()
              handleSearch()
            }
          }}
          placeholder='Enter a search query...'
          className='h-8 min-w-0 flex-1'
        />
        <Button
          size='icon'
          variant='outline'
          className='h-8 w-8'
          aria-label='Search'
          onClick={handleSearch}
          disabled={!canSubmit || isSearching}
        >
          {isSearching ? (
            <Loader2 className='animate-spin' />
          ) : (
            <SearchIcon />
          )}
        </Button>
        <Popover>
          <PopoverTrigger asChild>
            <Button
              variant='outline'
              size='icon'
              aria-label='Search filters'
              className='h-8 w-8'
            >
              <ListFilter className='size-4' />
            </Button>
          </PopoverTrigger>
          <PopoverContent align='end' className='w-72'>
            <SearchFilterPanel
              tasks={tasks ?? []}
              task={task}
              onTaskChange={setTask}
              top={top}
              onTopChange={setTop}
            />
          </PopoverContent>
        </Popover>
        <ThemeSwitch />
        <ConfigDrawer />
        <ProfileDropdown />
      </Header>

      <Main fluid className='flex flex-1 flex-col gap-6'>
        {diagnostics && <SearchDiagnosticsPanel diagnostics={diagnostics} />}

        <SearchResultsPanel
          hasSearched={hasSearched}
          mergedRows={mergedRows}
          collectionsResults={collectionsResults}
        />
      </Main>
    </>
  )
}
