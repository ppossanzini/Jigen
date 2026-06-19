import { computed, defineComponent, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import SecurityUsersToolbar from '@/modules/security/components/SecurityUsersToolbar/SecurityUsersToolbar.vue'
import SecurityUsersTable from '@/modules/security/components/SecurityUsersTable/SecurityUsersTable.vue'
import SecurityUsersDetailPanel from '@/modules/security/components/SecurityUsersDetailPanel/SecurityUsersDetailPanel.vue'
import { useSecurityStore, type SecurityUser } from '@/stores/security'

type UserDialogMode = 'create' | 'edit'

interface UserDialogForm {
  userName: string
  password: string
  roles: string[]
}

interface UserDialogErrors {
  userName: string
  password: string
}

export default defineComponent({
  name: 'SecurityUsersView',
  components: {
    SecurityUsersToolbar,
    SecurityUsersTable,
    SecurityUsersDetailPanel,
  },
  setup() {
    const { t } = useI18n()
    const securityStore = useSecurityStore()

    const userDialogVisible = ref(false)
    const userDialogMode = ref<UserDialogMode>('create')
    const userDialogSaving = ref(false)
    const applyingRoles = ref(false)
    const selectedRoles = ref<string[]>([])

    const userDialogForm = reactive<UserDialogForm>({
      userName: '',
      password: '',
      roles: [],
    })

    const userDialogErrors = reactive<UserDialogErrors>({
      userName: '',
      password: '',
    })

    const selectedUser = computed(() => securityStore.selectedUser)
    const selectedUserDetail = computed(() => securityStore.selectedUserDetail)
    const roleOptions = computed(() => securityStore.roles.map((entry) => entry.name))

    const users = computed(() => securityStore.users)
    const visibleUsers = users

    const userDialogTitle = computed(() =>
      userDialogMode.value === 'create' ? t('security.users.dialog.createTitle') : t('security.users.dialog.editTitle'),
    )

    const onSelectUser = async (user: SecurityUser) => {
      securityStore.setSelectedUser(user.id)
      await securityStore.loadUserDetail(user.id)
    }

    const refreshData = async () => {
      await Promise.all([securityStore.loadUsers(), securityStore.loadRoles()])

      if (!securityStore.selectedUserId) {
        const first = visibleUsers.value[0] ?? null

        if (first) {
          await onSelectUser(first)
        }

        return
      }

      await securityStore.loadUserDetail(securityStore.selectedUserId)
    }

    const resetDialogForm = () => {
      userDialogForm.userName = ''
      userDialogForm.password = ''
      userDialogForm.roles = []
      userDialogErrors.userName = ''
      userDialogErrors.password = ''
    }

    const onOpenCreateUserDialog = () => {
      userDialogMode.value = 'create'
      resetDialogForm()
      userDialogVisible.value = true
    }

    const onOpenEditUserDialog = () => {
      if (!selectedUserDetail.value) {
        ElMessage.warning(t('security.users.feedback.selectUser'))
        return
      }

      userDialogMode.value = 'edit'
      userDialogForm.userName = selectedUserDetail.value.userName
      userDialogForm.password = ''
      userDialogForm.roles = [...selectedUserDetail.value.roles]
      userDialogVisible.value = true
    }

    const onCloseUserDialog = () => {
      userDialogVisible.value = false
    }

    const saveDialogUser = async () => {
      userDialogErrors.userName = ''
      userDialogErrors.password = ''

      const normalizedUserName = userDialogForm.userName.trim()

      if (!normalizedUserName) {
        userDialogErrors.userName = t('security.users.validation.userNameRequired')
        return
      }

      if (userDialogMode.value === 'create' && !userDialogForm.password.trim()) {
        userDialogErrors.password = t('security.users.validation.passwordRequired')
        return
      }

      userDialogSaving.value = true

      try {
        if (userDialogMode.value === 'create') {
          await securityStore.createUser({
            userName: normalizedUserName,
            password: userDialogForm.password,
            roles: userDialogForm.roles,
          })
          ElMessage.success(t('security.users.feedback.created'))
        } else if (selectedUser.value) {
          const payload: server.security.UpdateUserData = {
            userName: normalizedUserName,
            roles: userDialogForm.roles,
          }

          const normalizedPassword = userDialogForm.password.trim()
          if (normalizedPassword) {
            payload.password = normalizedPassword
          }

          await securityStore.updateUser(selectedUser.value.id, payload)
          ElMessage.success(t('security.users.feedback.updated'))
        }

        userDialogVisible.value = false
        await refreshData()
      } catch {
        ElMessage.error(t('security.common.error'))
      } finally {
        userDialogSaving.value = false
      }
    }

    const onDeleteUser = async () => {
      if (!selectedUser.value) {
        ElMessage.warning(t('security.users.feedback.selectUser'))
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

        await securityStore.deleteUser(selectedUser.value.id)
        const nextSelected = users.value[0] ?? null

        if (nextSelected) {
          await onSelectUser(nextSelected)
        }

        ElMessage.success(t('security.users.feedback.deleted'))
      } catch {
        // Dialog cancellation is expected and should not show feedback.
      }
    }

    const onSelectedRolesChange = (roles: string[]) => {
      selectedRoles.value = roles
    }

    const onSaveRoles = async () => {
      if (!selectedUser.value) {
        ElMessage.warning(t('security.users.feedback.selectUser'))
        return
      }

      applyingRoles.value = true

      try {
        await securityStore.saveUserRoles(selectedUser.value.id, selectedRoles.value)
        ElMessage.success(t('security.users.feedback.rolesUpdated'))
      } catch {
        ElMessage.error(t('security.common.error'))
      } finally {
        applyingRoles.value = false
      }
    }

    const onRefresh = async () => {
      try {
        await refreshData()
        ElMessage.success(t('security.users.feedback.refreshed'))
      } catch {
        ElMessage.error(t('security.common.error'))
      }
    }

    watch(
      () => selectedUserDetail.value,
      (detail) => {
        selectedRoles.value = detail ? [...detail.roles] : []
      },
      { immediate: true },
    )

    onMounted(() => {
      void refreshData()
    })

    return {
      t,
      securityStore,
      visibleUsers,
      selectedUser,
      selectedUserDetail,
      roleOptions,
      userDialogVisible,
      userDialogTitle,
      userDialogForm,
      userDialogSaving,
      userDialogErrors,
      applyingRoles,
      selectedRoles,
      onSelectUser,
      onOpenCreateUserDialog,
      onOpenEditUserDialog,
      onCloseUserDialog,
      saveDialogUser,
      onDeleteUser,
      onSelectedRolesChange,
      onSaveRoles,
      onRefresh,
    }
  },
})
