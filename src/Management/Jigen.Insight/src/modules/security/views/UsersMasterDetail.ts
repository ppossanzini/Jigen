import { computed, defineComponent, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { SecurityUserApiModel } from '~types/security'
import { securityService } from '@/services/securityService'
import { useSecurityStore } from '@/stores/security'
import UsersMasterTable from '@/modules/security/components/UsersMasterTable/UsersMasterTable.vue'

interface UserDialogForm {
  userName: string
  password: string
  roles: string[]
}

export default defineComponent({
  name: 'UsersMasterDetail',
  components: {
    UsersMasterTable,
  },
  setup() {
    const { t } = useI18n()
    const securityStore = useSecurityStore()

    const currentPage = ref(1)
    const pageSize = ref(6)
    const applyingRoles = ref(false)
    const loadingUserDetail = ref(false)
    const userDialogVisible = ref(false)
    const userDialogSaving = ref(false)
    const editUserId = ref<string | null>(null)
    const selectedRoles = ref<string[]>([])

    const userDialogForm = reactive<UserDialogForm>({
      userName: '',
      password: '',
      roles: [],
    })

    const roleOptions = computed(() =>
      securityStore.roles
        .map((role) => role.name)
        .filter((roleName): roleName is string => typeof roleName === 'string' && roleName.length > 0),
    )

    const visibleUsers = computed(() => {
      const start = (currentPage.value - 1) * pageSize.value
      return securityStore.users.slice(start, start + pageSize.value)
    })

    const selectedUser = computed(() => securityStore.selectedUser)

    const usersCount = computed(() => securityStore.users.length)

    const userDialogTitle = computed(() =>
      editUserId.value ? t('security.users.dialog.editTitle') : t('security.users.dialog.createTitle'),
    )

    const ensureCurrentPage = () => {
      const maxPages = Math.max(1, Math.ceil(usersCount.value / pageSize.value))
      if (currentPage.value > maxPages) {
        currentPage.value = maxPages
      }
    }

    const calculateDynamicPageSize = () => {
      const reserved = 490
      const rowHeight = 52
      const available = Math.max(window.innerHeight - reserved, rowHeight * 4)
      pageSize.value = Math.max(4, Math.floor(available / rowHeight))
      ensureCurrentPage()
    }

    const onSelectUser = async (row: SecurityUserApiModel) => {
      securityStore.setSelectedUser(row.id)

      const selectedId = row.id
      loadingUserDetail.value = true

      try {
        const detail = await securityService.getUserById(selectedId)

        if (securityStore.selectedUserId !== selectedId) {
          return
        }

        const roles = detail.roles ?? []
        securityStore.setUserRoles(selectedId, [...roles])
        selectedRoles.value = [...roles]
      } catch {
        if (securityStore.selectedUserId !== selectedId) {
          return
        }

        // Keep previous cached mapping if detail endpoint fails.
        selectedRoles.value = [...(securityStore.userRolesById[selectedId] ?? [])]
      } finally {
        if (securityStore.selectedUserId === selectedId) {
          loadingUserDetail.value = false
        }
      }
    }

    const onUsersPageChange = (page: number) => {
      currentPage.value = page
      const nextSelected = visibleUsers.value[0]

      if (nextSelected) {
        void onSelectUser(nextSelected)
      }
    }

    const onOpenCreateUserDialog = () => {
      editUserId.value = null
      userDialogForm.userName = ''
      userDialogForm.password = ''
      userDialogForm.roles = []
      userDialogVisible.value = true
    }

    const onOpenEditUserDialog = () => {
      if (!selectedUser.value) {
        return
      }

      editUserId.value = selectedUser.value.id
      userDialogForm.userName = selectedUser.value.userName ?? ''
      userDialogForm.password = ''
      userDialogForm.roles = [...selectedRoles.value]
      userDialogVisible.value = true
    }

    const onCloseUserDialog = () => {
      userDialogVisible.value = false
    }

    const saveDialogUser = async () => {
      const normalizedUserName = userDialogForm.userName.trim()

      if (!normalizedUserName) {
        ElMessage.warning(t('security.users.validation.userNameRequired'))
        return
      }

      if (!editUserId.value && !userDialogForm.password) {
        ElMessage.warning(t('security.users.validation.passwordRequired'))
        return
      }

      userDialogSaving.value = true

      try {
        if (editUserId.value) {
          await securityService.updateUser(editUserId.value, {
            userName: normalizedUserName,
            password: userDialogForm.password || null,
            roles: [...userDialogForm.roles],
          })
          securityStore.setUserRoles(editUserId.value, [...userDialogForm.roles])
          ElMessage.success(t('security.users.feedback.updated'))
        } else {
          await securityService.createUser({
            userName: normalizedUserName,
            password: userDialogForm.password,
            roles: [...userDialogForm.roles],
          })
          ElMessage.success(t('security.users.feedback.created'))
        }

        await securityStore.loadUsers()
        ensureCurrentPage()
        userDialogVisible.value = false
      } catch {
        ElMessage.error(t('security.common.error'))
      } finally {
        userDialogSaving.value = false
      }
    }

    const onDeleteUser = async () => {
      if (!selectedUser.value) {
        return
      }

      try {
        await ElMessageBox.confirm(
          t('security.users.feedback.deleteConfirm'),
          t('security.common.warning'),
          {
            type: 'warning',
            confirmButtonText: t('security.common.delete'),
            cancelButtonText: t('security.common.cancel'),
          },
        )

        await securityService.deleteUser(selectedUser.value.id)
        delete securityStore.userRolesById[selectedUser.value.id]
        securityStore.setSelectedUser(null)
        await securityStore.loadUsers()
        ensureCurrentPage()

        const fallback = visibleUsers.value[0]
        if (fallback) {
          void onSelectUser(fallback)
        }

        ElMessage.success(t('security.users.feedback.deleted'))
      } catch {
        // Dialog cancellation is expected and should not show feedback.
      }
    }

    const onSaveRoles = async () => {
      if (!selectedUser.value) {
        return
      }

      applyingRoles.value = true

      try {
        await securityService.updateUser(selectedUser.value.id, {
          userName: selectedUser.value.userName,
          password: null,
          roles: [...selectedRoles.value],
        })

        securityStore.setUserRoles(selectedUser.value.id, [...selectedRoles.value])
        ElMessage.success(t('security.users.feedback.rolesUpdated'))
      } catch {
        ElMessage.error(t('security.common.error'))
      } finally {
        applyingRoles.value = false
      }
    }

    onMounted(async () => {
      try {
        await securityStore.loadAll()
        calculateDynamicPageSize()

        const firstUser = visibleUsers.value[0]
        if (firstUser) {
          void onSelectUser(firstUser)
        }
      } catch {
        ElMessage.error(t('security.common.error'))
      }

      window.addEventListener('resize', calculateDynamicPageSize)
    })

    onBeforeUnmount(() => {
      window.removeEventListener('resize', calculateDynamicPageSize)
    })

    return {
      t,
      securityStore,
      currentPage,
      pageSize,
      selectedUser,
      selectedRoles,
      visibleUsers,
      roleOptions,
      applyingRoles,
      loadingUserDetail,
      userDialogVisible,
      userDialogSaving,
      userDialogTitle,
      userDialogForm,
      onSelectUser,
      onUsersPageChange,
      onOpenCreateUserDialog,
      onOpenEditUserDialog,
      onCloseUserDialog,
      saveDialogUser,
      onDeleteUser,
      onSaveRoles,
    }
  },
})
