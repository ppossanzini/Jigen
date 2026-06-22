import { computed, defineComponent, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import DatabaseToolbar from '@/modules/jigen-db/components/DatabaseToolbar/DatabaseToolbar.vue'
import DatabaseTable from '@/modules/jigen-db/components/DatabaseTable/DatabaseTable.vue'
import DatabaseDetailPanel from '@/modules/jigen-db/components/DatabaseDetailPanel/DatabaseDetailPanel.vue'
import DatabaseCollectionsPanel from '@/modules/jigen-db/components/DatabaseCollectionsPanel/DatabaseCollectionsPanel.vue'
import CollectionExplorerPanel from '@/modules/jigen-db/components/CollectionExplorerPanel/CollectionExplorerPanel.vue'
import { useDatabaseStore } from '@/stores/database'
import { useAuthStore } from '@/stores/auth'
import { securityService } from '@/services/securityService'

interface AssignableUserOption {
  userId: string
  userName: string
}

interface DatabaseAssignedUser {
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
    const revokeAccessSaving = ref(false)
    const revokeAccessDialogVisible = ref(false)
    const revokeAccessAcknowledge = ref(false)
    const revokeAccessTargetUser = ref<DatabaseAssignedUser | null>(null)
    const assignableUsers = ref<AssignableUserOption[]>([])
    const selectedAssignableUserId = ref('')

    const createForm = reactive<CreateDatabaseForm>({
      name: '',
    })

    const canManageDatabases = computed(() => authStore.isDatabaseAdmin)
    const canManageDatabaseUsers = computed(() => authStore.isSecurityAdmin)

    const rows = computed<server.database.DatabaseName[]>(() => databaseStore.databases)
    const selectedRow = computed(() => databaseStore.selectedDatabase)
    const selectedDetails = computed(() => databaseStore.selectedDatabaseDetails)
    const selectedDatabaseCollections = computed(() => databaseStore.selectedDatabaseCollections)
    const selectedCollectionName = computed(() => databaseStore.selectedCollectionName)
    const selectedCollection = computed(() => databaseStore.selectedCollection)
    const collectionsCountByDatabase = computed<Record<string, number>>(() => {
      const counts: Record<string, number> = {}

      for (const name of databaseStore.databases) {
        const detailsCount = databaseStore.detailsByDatabase[name]?.collectionsCount
        const loadedCollectionsCount = databaseStore.collectionsByDatabase[name]?.length

        counts[name] = typeof detailsCount === 'number'
          ? detailsCount
          : typeof loadedCollectionsCount === 'number'
            ? loadedCollectionsCount
            : 0
      }

      return counts
    })

    const showCollectionsPanel = computed(() => Boolean(selectedRow.value))
    const showCollectionDetailsPanel = computed(() => Boolean(selectedCollection.value))
    const workspaceGridClass = computed(() => ({
      'has-database': showCollectionsPanel.value,
      'has-collection': showCollectionDetailsPanel.value,
    }))

    const onRowClick = async (row: server.database.DatabaseName) => {
      databaseStore.setSelectedDatabase(row)
      await Promise.all([
        databaseStore.loadCollectionsFor(row),
        databaseStore.loadDetailsFor(row),
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

    const onDeleteDatabase = async (row?: server.database.DatabaseName) => {
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
          t('databaseManagement.feedback.deleteConfirm', { name: target }),
          t('databaseManagement.warning'),
          {
            type: 'warning',
            confirmButtonText: t('databaseManagement.deleteDatabase'),
            cancelButtonText: t('databaseManagement.cancel'),
          },
        )

        await databaseStore.deleteDatabase(target)

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
      if (!canManageDatabaseUsers.value) {
        ElMessage.warning(t('databaseManagement.feedback.securityAdminOnly'))
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
        (selectedDetails.value?.users ?? []).some((entry) => entry.userId === targetUserId)

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
      const usersById = new Map<string, DatabaseAssignedUser>()

      for (const user of currentUsers) {
        if (!user.userId) {
          continue
        }

        usersById.set(user.userId, {
          userId: user.userId,
          userName: user.userName ?? '',
        })
      }

      usersById.set(selectedUser.userId, {
        userId: selectedUser.userId,
        userName: selectedUser.userName,
      })

      assignUserSaving.value = true

      try {
        await databaseStore.setDatabaseUsers(targetDatabase, Array.from(usersById.values()))
        selectedAssignableUserId.value = ''
        ElMessage.success(t('databaseManagement.feedback.userAssigned'))
      } catch {
        ElMessage.error(t('databaseManagement.feedback.error'))
      } finally {
        assignUserSaving.value = false
      }
    }

    const onOpenRevokeAccessDialog = (user: DatabaseAssignedUser) => {
      if (!canManageDatabaseUsers.value) {
        ElMessage.warning(t('databaseManagement.feedback.securityAdminOnly'))
        return
      }

      if (!selectedRow.value) {
        ElMessage.warning(t('databaseManagement.feedback.selectDatabase'))
        return
      }

      revokeAccessTargetUser.value = {
        userId: user.userId,
        userName: user.userName,
      }
      revokeAccessAcknowledge.value = false
      revokeAccessDialogVisible.value = true
    }

    const onCloseRevokeAccessDialog = () => {
      revokeAccessDialogVisible.value = false
      revokeAccessAcknowledge.value = false
      revokeAccessTargetUser.value = null
    }

    const onConfirmRevokeAccess = async () => {
      if (!canManageDatabaseUsers.value) {
        ElMessage.warning(t('databaseManagement.feedback.securityAdminOnly'))
        return
      }

      const targetDatabase = selectedRow.value
      const targetUser = revokeAccessTargetUser.value

      if (!targetDatabase) {
        ElMessage.warning(t('databaseManagement.feedback.selectDatabase'))
        return
      }

      if (!targetUser?.userId) {
        ElMessage.warning(t('databaseManagement.feedback.selectUser'))
        return
      }

      const currentUsers = selectedDetails.value?.users ?? []
      const filteredUsers = currentUsers.filter((entry) => entry.userId && entry.userId !== targetUser.userId)
      const normalizedFilteredUsers = filteredUsers.map((entry) => ({
        userId: entry.userId as string,
        userName: entry.userName ?? '',
      }))

      if (filteredUsers.length === currentUsers.length) {
        ElMessage.info(t('databaseManagement.feedback.userNotFoundInDatabase'))
        return
      }

      revokeAccessSaving.value = true

      try {
        await databaseStore.setDatabaseUsers(targetDatabase, normalizedFilteredUsers)
        ElMessage.success(t('databaseManagement.feedback.userRemoved'))
        onCloseRevokeAccessDialog()
      } catch {
        ElMessage.error(t('databaseManagement.feedback.error'))
      } finally {
        revokeAccessSaving.value = false
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
      collectionsCountByDatabase,
      showCollectionsPanel,
      showCollectionDetailsPanel,
      workspaceGridClass,
      canManageDatabases,
      canManageDatabaseUsers,
      createDialogVisible,
      createSaving,
      assignUserSaving,
      revokeAccessSaving,
      revokeAccessDialogVisible,
      revokeAccessAcknowledge,
      revokeAccessTargetUser,
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
      onOpenRevokeAccessDialog,
      onCloseRevokeAccessDialog,
      onConfirmRevokeAccess,
    }
  },
})
