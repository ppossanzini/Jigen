import { useState } from 'react'
import { getRouteApi } from '@tanstack/react-router'
import { formatBytes, formatNumber } from '@/lib/utils'
import { ConfigDrawer } from '@/components/config-drawer'
import { ConfigSidebar } from '@/components/layout/config-sidebar'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { ThemeSwitch } from '@/components/theme-switch'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import type { CollectionGraphParams } from '@/features/collections/api'
import {
  useCollectionGraph,
  useCollectionInfo,
  useCollections,
} from '@/features/collections/hooks'
import { useDatabases } from '@/features/databases/hooks'
import { GraphControls } from './components/graph-controls'
import { GraphRenderControls } from './components/graph-render-controls'
import { GraphViewer2D } from './components/graph-viewer-2d'
import { GraphViewer3D } from './components/graph-viewer-3d'

const route = getRouteApi('/_authenticated/graph-explorer/')

export function GraphExplorer() {
  const { db, collection: collectionParam } = route.useSearch()
  const navigate = route.useNavigate()

  const [dbname, setDbname] = useState<string | null>(db ?? null)
  const [collection, setCollection] = useState<string | null>(
    collectionParam ?? null
  )
  const [params, setParams] = useState<CollectionGraphParams>({
    dimensions: 2,
    limit: 500,
  })

  const { data: databases } = useDatabases()
  const { data: collections } = useCollections(dbname)
  const { data: collectionInfo } = useCollectionInfo(dbname, collection)
  const { data: graph, isLoading: graphLoading } = useCollectionGraph(
    dbname,
    collection,
    params
  )

  const returnedNodeCount = graph?.nodes?.length ?? 0
  const [renderLimit, setRenderLimit] = useState(returnedNodeCount)
  const [pointScale, setPointScale] = useState(1)

  // Reset the client-side render cap whenever a new graph snapshot comes in,
  // so it always starts at "show everything the server returned". Adjusted
  // during render (not an effect) to avoid an extra cascading render.
  const [trackedNodeCount, setTrackedNodeCount] = useState(returnedNodeCount)
  if (returnedNodeCount !== trackedNodeCount) {
    setTrackedNodeCount(returnedNodeCount)
    setRenderLimit(returnedNodeCount)
  }

  function handleDbnameChange(next: string) {
    setDbname(next)
    setCollection(null)
    navigate({
      search: (prev) => ({ ...prev, db: next, collection: undefined }),
      replace: true,
    })
  }

  function handleCollectionChange(next: string) {
    setCollection(next)
    navigate({
      search: (prev) => ({ ...prev, collection: next }),
      replace: true,
    })
  }

  return (
    <>
      <Header fixed>
        <div className='me-auto' />
        <ThemeSwitch />
        <ConfigDrawer />
        <ProfileDropdown />
      </Header>

      <Main fixed fluid className='flex flex-1 flex-col gap-4 lg:flex-row'>
        <div className='flex min-h-0 min-w-0 flex-1 flex-col gap-4'>
          {!collection ? (
            <p className='text-muted-foreground text-sm'>
              Select a database and collection to explore its index graph.
            </p>
          ) : graphLoading ? (
            <Skeleton className='h-96 w-full' />
          ) : (
            <>
              <div className='flex flex-wrap gap-2'>
                <Badge variant='outline'>
                  Nodes: {formatNumber(graph?.totalNodes)}
                </Badge>
                <Badge variant='outline'>
                  Live: {formatNumber(graph?.liveNodes)}
                </Badge>
                <Badge variant='outline'>
                  Deleted: {formatNumber(graph?.deletedNodes)}
                </Badge>
                <Badge variant='outline'>
                  Shown: {formatNumber(graph?.returnedNodes)}
                </Badge>
                <Badge variant='outline'>
                  Max level: {formatNumber(graph?.maxLevel)}
                </Badge>
                <Badge variant='outline'>
                  Entrypoint: {formatNumber(graph?.entrypointPositionId)}
                </Badge>
                {graph?.truncated && (
                  <Badge variant='destructive'>Truncated</Badge>
                )}
                {collectionInfo?.index && (
                  <>
                    <Badge variant='outline'>
                      Avg degree:{' '}
                      {Number(collectionInfo.index.averageDegree ?? 0).toFixed(
                        2
                      )}
                    </Badge>
                    <Badge variant='outline'>
                      Index size:{' '}
                      {formatBytes(collectionInfo.index.indexSizeBytes)}
                    </Badge>
                    {collectionInfo.index.quantization && (
                      <Badge variant='outline'>
                        {collectionInfo.index.quantization}
                      </Badge>
                    )}
                  </>
                )}
              </div>

              <div className='min-h-0 flex-1 rounded-md border'>
                {params.dimensions === 3 ? (
                  <GraphViewer3D
                    graph={graph ?? null}
                    nodeLimit={renderLimit}
                    pointScale={pointScale}
                  />
                ) : (
                  <GraphViewer2D
                    graph={graph ?? null}
                    nodeLimit={renderLimit}
                    pointScale={pointScale}
                  />
                )}
              </div>
            </>
          )}
        </div>

        <ConfigSidebar>
          <GraphControls
            databases={databases ?? []}
            dbname={dbname}
            onDbnameChange={handleDbnameChange}
            collections={collections ?? []}
            collection={collection}
            onCollectionChange={handleCollectionChange}
            params={params}
            onParamsChange={setParams}
          />

          {collection && !graphLoading && (
            <>
              <Separator />
              <GraphRenderControls
                nodeCount={returnedNodeCount}
                nodeLimit={renderLimit}
                onNodeLimitChange={setRenderLimit}
                pointScale={pointScale}
                onPointScaleChange={setPointScale}
              />
            </>
          )}
        </ConfigSidebar>
      </Main>
    </>
  )
}
