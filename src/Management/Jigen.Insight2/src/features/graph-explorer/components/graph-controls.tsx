import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { CollectionGraphParams } from '@/features/collections/api'

type GraphControlsProps = {
  databases: string[]
  dbname: string | null
  onDbnameChange: (dbname: string) => void
  collections: string[]
  collection: string | null
  onCollectionChange: (collection: string) => void
  params: CollectionGraphParams
  onParamsChange: (params: CollectionGraphParams) => void
}

export function GraphControls({
  databases,
  dbname,
  onDbnameChange,
  collections,
  collection,
  onCollectionChange,
  params,
  onParamsChange,
}: GraphControlsProps) {
  return (
    <div className='space-y-4'>
      <div className='space-y-2'>
        <Label>Database</Label>
        <Select value={dbname ?? undefined} onValueChange={onDbnameChange}>
          <SelectTrigger className='w-full'>
            <SelectValue placeholder='Select a database' />
          </SelectTrigger>
          <SelectContent>
            {databases.map((name) => (
              <SelectItem key={name} value={name}>
                {name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className='space-y-2'>
        <Label>Collection</Label>
        <Select
          value={collection ?? undefined}
          onValueChange={onCollectionChange}
          disabled={!dbname}
        >
          <SelectTrigger className='w-full'>
            <SelectValue placeholder='Select a collection' />
          </SelectTrigger>
          <SelectContent>
            {collections.map((name) => (
              <SelectItem key={name} value={name}>
                {name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className='space-y-2'>
        <Label>Dimensions</Label>
        <Select
          value={String(params.dimensions ?? 2)}
          onValueChange={(value) =>
            onParamsChange({ ...params, dimensions: Number(value) })
          }
        >
          <SelectTrigger className='w-full'>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value='2'>2D</SelectItem>
            <SelectItem value='3'>3D</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className='space-y-2'>
        <Label>Node limit</Label>
        <Input
          type='number'
          min={1}
          max={20000}
          value={params.limit ?? 500}
          onChange={(event) =>
            onParamsChange({
              ...params,
              limit: Number(event.target.value) || 1,
            })
          }
        />
      </div>
    </div>
  )
}
