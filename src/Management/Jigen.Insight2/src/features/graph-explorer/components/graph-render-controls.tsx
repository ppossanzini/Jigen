import { Label } from '@/components/ui/label'

type GraphRenderControlsProps = {
  nodeCount: number
  nodeLimit: number
  onNodeLimitChange: (limit: number) => void
  pointScale: number
  onPointScaleChange: (scale: number) => void
}

export function GraphRenderControls({
  nodeCount,
  nodeLimit,
  onNodeLimitChange,
  pointScale,
  onPointScaleChange,
}: GraphRenderControlsProps) {
  return (
    <div className='space-y-4'>
      <div className='space-y-2'>
        <Label>
          Rendered points: {nodeLimit} / {nodeCount}
        </Label>
        <input
          type='range'
          min={Math.min(10, nodeCount)}
          max={Math.max(nodeCount, 1)}
          step={1}
          value={Math.min(nodeLimit, Math.max(nodeCount, 1))}
          onChange={(event) => onNodeLimitChange(Number(event.target.value))}
          className='accent-primary w-full'
          disabled={nodeCount <= 1}
        />
      </div>

      <div className='space-y-2'>
        <Label>Point size: {pointScale.toFixed(1)}x</Label>
        <input
          type='range'
          min={0.2}
          max={2}
          step={0.1}
          value={pointScale}
          onChange={(event) => onPointScaleChange(Number(event.target.value))}
          className='accent-primary w-full'
        />
      </div>
    </div>
  )
}
