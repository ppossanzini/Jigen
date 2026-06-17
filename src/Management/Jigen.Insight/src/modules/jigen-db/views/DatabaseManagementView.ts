import { computed, defineComponent, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import DatabaseToolbar from '@/modules/jigen-db/components/DatabaseToolbar/DatabaseToolbar.vue'
import DatabaseTable from '@/modules/jigen-db/components/DatabaseTable/DatabaseTable.vue'
import DatabaseDetailPanel from '@/modules/jigen-db/components/DatabaseDetailPanel/DatabaseDetailPanel.vue'
import type { DatabaseRow } from '@/modules/jigen-db/types'
import { useDatabaseStore } from '@/stores/database'
import { useAuthStore } from '@/stores/auth'

interface CreateDatabaseForm {
  name: string
}

export default defineComponent({
  name: 'DatabaseManagementView',
  components: {
    DatabaseToolbar,
    DatabaseTable,
    DatabaseDetailPanel,
  },
  setup() {
    const { t } = useI18n()
    const databaseStore = useDatabaseStore()
    const authStore = useAuthStore()

    const currentPage = ref(1)
    const pageSize = ref(6)
    const createDialogVisible = ref(false)
    const createSaving = ref(false)

    const createForm = reactive<CreateDatabaseForm>({
      name: '',
    })

    const canManageDatabases = computed(() => authStore.isDatabaseAdmin)

    const rows = computed<DatabaseRow[]>(() => databaseStore.databases)
    const selectedRow = computed(() => databaseStore.selectedDatabase)
    const selectedCollections = computed(() => databaseStore.selectedCollections)

    const calculateDynamicPageSize = () => {
      const reservedSpace = 470
      const rowHeight = 54
      const availableHeight = Math.max(window.innerHeight - reservedSpace, rowHeight * 5)
      pageSize.value = Math.max(4, Math.floor(availableHeight / rowHeight))
      const maxPages = Math.max(1, Math.ceil(rows.value.length / pageSize.value))
      if (currentPage.value > maxPages) currentPage.value = maxPages
    }

    const visibleRows = computed(() => {
      const start = (currentPage.value - 1) * pageSize.value
      return rows.value.slice(start, start + pageSize.value)
    })

    const onRowClick = async (row: DatabaseRow) => {
      databaseStore.setSelectedDatabase(row.name)
      await databaseStore.loadCollectionsFor(row.name)
    }

    const onPageChange = (nextPage: number) => {
      currentPage.value = nextPage
      const fallback = visibleRows.value[0] ?? null

      if (!fallback) {
        databaseStore.setSelectedDatabase(null)
        return
      }

      void onRowClick(fallback)
    }

    const refreshDatabases = async () => {
      await databaseStore.loadDatabases()

      if (!databaseStore.selectedDatabaseName) {
        const first = visibleRows.value[0] ?? null

        if (!first) {
          return
        }

        await onRowClick(first)
        return
      }

      await databaseStore.loadCollectionsFor(databaseStore.selectedDatabaseName)
    }

    const onOpenCreateDialog = () => {
      if (!canManageDatabases.value) {
        ElMessage.warning(t('databaseManagement.feedback.adminOnly'))
        return
      }

      createForm.name = ''
      createDialogVisible.value = true
    }

    const onCloseCreateDialog = () => {
      createDialogVisible.value = false
    }

    const onCreateDatabase = async () => {
      if (!canManageDatabases.value) {
        ElMessage.warning(t('databaseManagement.feedback.adminOnly'))
        return
      }

      const normalizedName = createForm.name.trim()

      if (!normalizedName) {
        ElMessage.warning(t('databaseManagement.validation.nameRequired'))
        return
      }

      createSaving.value = true

      try {
        await databaseStore.createDatabase(normalizedName)
        databaseStore.setSelectedDatabase(normalizedName)
        await databaseStore.loadCollectionsFor(normalizedName)
        createDialogVisible.value = false
        ElMessage.success(t('databaseManagement.feedback.created'))
      } catch {
        ElMessage.error(t('databaseManagement.feedback.error'))
      } finally {
        createSaving.value = false
      }
    }

    const onDeleteDatabase = async (row?: DatabaseRow) => {
      if (!canManageDatabases.value) {
        ElMessage.warning(t('databaseManagement.feedback.adminOnly'))
        return
      }

      const target = row ?? selectedRow.value

      if (!target) {
        ElMessage.warning(t('databaseManagement.feedback.selectDatabase'))
        return
      }

      try {
        await ElMessageBox.confirm(
          t('databaseManagement.feedback.deleteConfirm', { name: target.name }),
          t('databaseManagement.warning'),
          {
            type: 'warning',
            confirmButtonText: t('databaseManagement.deleteDatabase'),
            cancelButtonText: t('databaseManagement.cancel'),
          },
        )

        await databaseStore.deleteDatabase(target.name)

        const nextSelected = visibleRows.value[0] ?? null
        if (nextSelected) {
          await onRowClick(nextSelected)
        }

        ElMessage.success(t('databaseManagement.feedback.deleted'))
      } catch {
        // Dialog cancellation is expected and should not show feedback.
      }
    }

    const onReadCollections = async (row: DatabaseRow) => {
      await onRowClick(row)
    }

    const onRefresh = async () => {
      try {
        await refreshDatabases()
        ElMessage.success(t('databaseManagement.feedback.refreshed'))
      } catch {
        ElMessage.error(t('databaseManagement.feedback.error'))
      }
    }

    onMounted(() => {
      void refreshDatabases()
      window.addEventListener('resize', calculateDynamicPageSize)
      calculateDynamicPageSize()
    })

    onBeforeUnmount(() => {
      window.removeEventListener('resize', calculateDynamicPageSize)
    })

    return {
      t,
      databaseStore,
      rows,
      selectedRow,
      selectedCollections,
      canManageDatabases,
      currentPage,
      pageSize,
      createDialogVisible,
      createSaving,
      createForm,
      visibleRows,
      onRowClick,
      onPageChange,
      onRefresh,
      onReadCollections,
      onOpenCreateDialog,
      onCloseCreateDialog,
      onCreateDatabase,
      onDeleteDatabase,
    }
  },
})
