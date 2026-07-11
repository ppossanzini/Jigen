import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

type SearchFilterPanelProps = {
  tasks: string[]
  task: string | null
  onTaskChange: (task: string | null) => void
  top: number
  onTopChange: (top: number) => void
}

export function SearchFilterPanel({
  tasks,
  task,
  onTaskChange,
  top,
  onTopChange,
}: SearchFilterPanelProps) {
  return (
    <div className='space-y-4'>
      <div className='space-y-2'>
        <Label>Embedding task (optional)</Label>
        <Select
          value={task ?? '__default__'}
          onValueChange={(value) =>
            onTaskChange(value === '__default__' ? null : value)
          }
        >
          <SelectTrigger className='w-full'>
            <SelectValue placeholder='Default' />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value='__default__'>Default</SelectItem>
            {tasks.map((t) => (
              <SelectItem key={t} value={t}>
                {t}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className='space-y-2'>
        <Label>Top-K</Label>
        <Input
          type='number'
          min={1}
          max={100}
          value={top}
          onChange={(event) => onTopChange(Number(event.target.value) || 1)}
        />
      </div>
    </div>
  )
}
