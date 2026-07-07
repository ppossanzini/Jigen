import { computed, defineComponent, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { databaseService } from '@/services/databaseService'
import { useDatabaseStore } from '@/stores/database'
import GraphViewer2D from '@/modules/jigen-db/components/GraphViewer2D/GraphViewer2D.vue'
import GraphViewer3D from '@/modules/jigen-db/components/GraphViewer3D/GraphViewer3D.vue'

const NODE_LIMIT_DEFAULT = 2000
const NODE_LIMIT_MIN = 50
const NODE_LIMIT_MAX = 20000
const NODE_LIMIT_STEP = 500
const ALL_LAYERS = -1

export default defineComponent({
  name: 'GraphExplorerView',
  components: {
    GraphViewer2D,
    GraphViewer3D,
  },
  setup() {
    const { t } = useI18n()
    const databaseStore = useDatabaseStore()

    const selectedDatabaseName = ref<string | null>(null)
    const selectedCollection = ref<string | null>(null)
    const viewMode = ref<'2d' | '3d'>('2d')
    const nodeLimit = ref(NODE_LIMIT_DEFAULT)
    const selectedLevel = ref<number>(ALL_LAYERS)
    const forceLayout = ref(true)
    const loading = ref(false)
    const graph = ref<server.database.CollectionGraph | null>(null)

    const databaseNames = computed(() => databaseStore.databases)
    const collectionsLoading = computed(() => databaseStore.loadingCollections)

    const availableCollections = computed(() => {
      if (!selectedDatabaseName.value) return []
      return databaseStore.collectionsByDatabase[selectedDatabaseName.value] ?? []
    })

    const levelOptions = computed(() => {
      const max = graph.value?.maxLevel ?? 0
      return [
        { label: t('graphExplorer.allLayers'), value: ALL_LAYERS },
        ...Array.from({ length: max + 1 }, (_, level) => ({ label: `L${level}`, value: level })),
      ]
    })

    const hasGraph = computed(() => Boolean(graph.value))
    const hasNodes = computed(() => (graph.value?.returnedNodes ?? 0) > 0)

    const loadGraph = async () => {
      if (!selectedDatabaseName.value || !selectedCollection.value) return

      loading.value = true
      try {
        graph.value = await databaseService.getCollectionGraph(
          selectedDatabaseName.value,
          selectedCollection.value,
          {
            dimensions: viewMode.value === '3d' ? 3 : 2,
            limit: nodeLimit.value,
            level: selectedLevel.value === ALL_LAYERS ? null : selectedLevel.value,
          },
        )
      } catch {
        ElMessage.error(t('graphExplorer.feedback.loadFailed'))
        graph.value = null
      } finally {
        loading.value = false
      }
    }

    watch(selectedDatabaseName, async (databaseName) => {
      selectedCollection.value = null
      graph.value = null

      if (!databaseName) return
      await databaseStore.loadCollectionsFor(databaseName)
    })

    watch(selectedCollection, () => {
      graph.value = null
    })

    watch([viewMode, selectedLevel], () => {
      if (graph.value) void loadGraph()
    })

    onMounted(async () => {
      await databaseStore.loadDatabases()
      if (!selectedDatabaseName.value) {
        selectedDatabaseName.value = databaseNames.value[0] ?? null
      }
    })

    return {
      t,
      databaseStore,
      selectedDatabaseName,
      selectedCollection,
      viewMode,
      nodeLimit,
      nodeLimitMin: NODE_LIMIT_MIN,
      nodeLimitMax: NODE_LIMIT_MAX,
      nodeLimitStep: NODE_LIMIT_STEP,
      selectedLevel,
      forceLayout,
      loading,
      graph,
      databaseNames,
      collectionsLoading,
      availableCollections,
      levelOptions,
      hasGraph,
      hasNodes,
      loadGraph,
    }
  },
})
