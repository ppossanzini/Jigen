import { ChevronDown } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

type SearchCollectionsSelectProps = {
  dbname: string | null
  collections: string[]
  collectionsLoading: boolean
  selectedCollections: string[]
  onToggleCollection: (collection: string) => void
}

export function SearchCollectionsSelect({
  dbname,
  collections,
  collectionsLoading,
  selectedCollections,
  onToggleCollection,
}: SearchCollectionsSelectProps) {
  const label =
    selectedCollections.length === 0
      ? 'Collections'
      : selectedCollections.length === 1
        ? selectedCollections[0]
        : `${selectedCollections.length} collections`

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          variant='outline'
          className='h-8 w-40 justify-between font-normal sm:w-52'
          disabled={!dbname || collectionsLoading}
        >
          <span className='truncate'>
            {collectionsLoading ? 'Loading...' : label}
          </span>
          <ChevronDown className='size-4 shrink-0 opacity-50' />
        </Button>
      </PopoverTrigger>
      <PopoverContent align='start' className='w-56 p-2'>
        {!dbname ? (
          <p className='text-muted-foreground p-2 text-sm'>
            Select a database first.
          </p>
        ) : collections.length === 0 ? (
          <p className='text-muted-foreground p-2 text-sm'>
            This database has no collections.
          </p>
        ) : (
          <div className='max-h-64 space-y-1 overflow-y-auto'>
            {collections.map((collection) => (
              <label
                key={collection}
                className='flex items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent'
              >
                <Checkbox
                  checked={selectedCollections.includes(collection)}
                  onCheckedChange={() => onToggleCollection(collection)}
                />
                {collection}
              </label>
            ))}
          </div>
        )}
      </PopoverContent>
    </Popover>
  )
}
