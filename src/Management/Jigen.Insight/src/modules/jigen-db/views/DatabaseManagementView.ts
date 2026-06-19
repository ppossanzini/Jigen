import { computed, defineComponent, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import DatabaseToolbar from '@/modules/jigen-db/components/DatabaseToolbar/DatabaseToolbar.vue'
import DatabaseTable from '@/modules/jigen-db/components/DatabaseTable/DatabaseTable.vue'
import DatabaseDetailPanel from '@/modules/jigen-db/components/DatabaseDetailPanel/DatabaseDetailPanel.vue'
import DatabaseCollectionsPanel from '@/modules/jigen-db/components/DatabaseCollectionsPanel/DatabaseCollectionsPanel.vue'
import CollectionExplorerPanel from '@/modules/jigen-db/components/CollectionExplorerPanel/CollectionExplorerPanel.vue'
import type { DatabaseRow } from '@/modules/jigen-db/types'
import { useDatabaseStore } from '@/stores/database'
import { useAuthStore } from '@/stores/auth'
import { securityService } from '@/services/securityService'

interface AssignableUserOption {
  userId: string
  userName: string
}

interface CreateDatabaseForm {
  name: string
}

export default defineComponent({
  name: 'DatabaseManagementView',
  components: {
    DatabaseToolbar,
    DatabaseTable,
    DatabaseDetailPanel,
    DatabaseCollectionsPanel,
    CollectionExplorerPanel,
  },
  setup() {
    const { t } = useI18n()
    const databaseStore = useDatabaseStore()
    const authStore = useAuthStore()

    const createDialogVisible = ref(false)
    const createSaving = ref(false)
    const assignUserSaving = ref(false)
    const assignableUsers = ref<AssignableUserOption[]>([])
    const selectedAssignableUserId = ref('')

    const createForm = reactive<CreateDatabaseForm>({
      name: '',
    })

    const canManageDatabases = computed(() => authStore.isDatabaseAdmin)

    const rows = computed<DatabaseRow[]>(() => databaseStore.databases)
    const selectedRow = computed(() => databaseStore.selectedDatabase)
    const selectedDetails = computed(() => databaseStore.selectedDatabaseDetails)
    const selectedDatabaseCollections = computed(() => databaseStore.selectedDatabaseCollections)
    const selectedCollectionName = computed(() => databaseStore.selectedCollectionName)
    const selectedCollection = computed(() => databaseStore.selectedCollection)

    const showCollectionsPanel = computed(() => Boolean(selectedRow.value))
    const showCollectionDetailsPanel = computed(() => Boolean(selectedCollection.value))
    const workspaceGridClass = computed(() => ({
      'has-database': showCollectionsPanel.value,
      'has-collection': showCollectionDetailsPanel.value,
    }))

    const onRowClick = async (row: DatabaseRow) => {
      databaseStore.setSelectedDatabase(row.name)
      await Promise.all([
        databaseStore.loadCollectionsFor(row.name),
        databaseStore.loadDetailsFor(row.name),
      ])
    }

    const onSelectCollection = (collectionName: string) => {
      databaseStore.setSelectedCollection(collectionName)
    }

    const loadAssignableUsers = async () => {
      try {
        const users = await securityService.listUsers()
        assignableUsers.value = users
          .map((entry) => ({
            userId: entry.id?.trim() ?? '',
            userName: entry.userName?.trim() ?? '',
          }))
          .filter((entry) => entry.userId.length > 0)
      } catch {
        assignableUsers.value = []
      }
    }

    const refreshDatabases = async () => {
      await databaseStore.loadDatabases()

      if (!databaseStore.selectedDatabaseName) {
        const first = rows.value[0] ?? null

        if (!first) {
          return
        }

        await onRowClick(first)
        return
      }

      await Promise.all([
        databaseStore.loadCollectionsFor(databaseStore.selectedDatabaseName),
        databaseStore.loadDetailsFor(databaseStore.selectedDatabaseName),
      ])
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
        await Promise.all([
          databaseStore.loadCollectionsFor(normalizedName),
          databaseStore.loadDetailsFor(normalizedName),
        ])
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

        const nextSelected = rows.value[0] ?? null
        if (nextSelected) {
          await onRowClick(nextSelected)
        }

        ElMessage.success(t('databaseManagement.feedback.deleted'))
      } catch {
        // Dialog cancellation is expected and should not show feedback.
      }
    }

    const onRefresh = async () => {
      try {
        await Promise.all([refreshDatabases(), loadAssignableUsers()])
        ElMessage.success(t('databaseManagement.feedback.refreshed'))
      } catch {
        ElMessage.error(t('databaseManagement.feedback.error'))
      }
    }

    const onAssignUserToDatabase = async () => {
      if (!canManageDatabases.value) {
        ElMessage.warning(t('databaseManagement.feedback.adminOnly'))
        return
      }

      const targetDatabase = selectedRow.value
      const targetUserId = selectedAssignableUserId.value.trim()

      if (!targetDatabase) {
        ElMessage.warning(t('databaseManagement.feedback.selectDatabase'))
        return
      }

      if (!targetUserId) {
        ElMessage.warning(t('databaseManagement.feedback.selectUser'))
        return
      }

      const alreadyAssigned =
        selectedDetails.value?.users.some((entry) => entry.userId === targetUserId) ?? false

      if (alreadyAssigned) {
        ElMessage.info(t('databaseManagement.feedback.userAlreadyAssigned'))
        return
      }

      const selectedUser = assignableUsers.value.find((entry) => entry.userId === targetUserId)
      if (!selectedUser) {
        ElMessage.warning(t('databaseManagement.feedback.selectUser'))
        return
      }

      const currentUsers = selectedDetails.value?.users ?? []
      const usersById = new Map<string, { userId: string; userName: string }>()

      for (const user of currentUsers) {
        if (!user.userId) {
          continue
        }

        usersById.set(user.userId, {
          userId: user.userId,
          userName: user.userName,
        })
      }

      usersById.set(selectedUser.userId, {
        userId: selectedUser.userId,
        userName: selectedUser.userName,
      })

      assignUserSaving.value = true

      try {
        await databaseStore.setDatabaseUsers(targetDatabase.name, Array.from(usersById.values()))
        selectedAssignableUserId.value = ''
        ElMessage.success(t('databaseManagement.feedback.userAssigned'))
      } catch {
        ElMessage.error(t('databaseManagement.feedback.error'))
      } finally {
        assignUserSaving.value = false
      }
    }

    onMounted(() => {
      void Promise.all([refreshDatabases(), loadAssignableUsers()])
    })

    return {
      t,
      databaseStore,
      rows,
      selectedRow,
      selectedDetails,
      selectedDatabaseCollections,
      selectedCollectionName,
      selectedCollection,
      showCollectionsPanel,
      showCollectionDetailsPanel,
      workspaceGridClass,
      canManageDatabases,
      createDialogVisible,
      createSaving,
      assignUserSaving,
      assignableUsers,
      selectedAssignableUserId,
      createForm,
      visibleRows: rows,
      onRowClick,
      onSelectCollection,
      onRefresh,
      onOpenCreateDialog,
      onCloseCreateDialog,
      onCreateDatabase,
      onDeleteDatabase,
      onAssignUserToDatabase,
    }
  },
})
