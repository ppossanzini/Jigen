import { defineComponent, ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import { SuccessFilled, CircleCloseFilled } from '@element-plus/icons-vue'

import { useUsersRolesStore } from '@/stores/usersRoles'
import type { UserItem, RoleItem } from '@/modules/users/types'

interface UserFormState {
  userName: string
  password: string
  roles: string[]
}

export default defineComponent({
  name: 'UsersMasterDetail',
  components: {
    SuccessFilled,
    CircleCloseFilled,
  },
  setup() {
    const { t } = useI18n()
    const store = useUsersRolesStore()

    const selectedUser = ref<UserItem | null>(null)
    const userDialogVisible = ref(false)
    const rolesDialogVisible = ref(false)
    const savingUser = ref(false)
    const savingRoles = ref(false)
    const rolesTreeRef = ref()

    const userForm = ref<UserFormState>({
      userName: '',
      password: '',
      roles: [],
    })

    const isUserCreateMode = computed(() => !selectedUser.value)
    const userDialogTitle = computed(() =>
      isUserCreateMode.value
        ? t('security.users.dialog.createTitle')
        : t('security.users.dialog.editTitle')
    )

    const userRolesData = computed(() =>
      store.roles.map((role) => ({
        ...role,
        isAssigned: selectedUser.value?.roles.includes(role.name) || false,
      }))
    )

    const rolesTreeData = computed(() => [
      {
        id: 'roles-group',
        name: t('security.roles.title'),
        children: store.roles.map((role) => ({
          id: role.name,
          name: role.name,
        })),
      },
    ])

    const canEdit = computed(() => Boolean(selectedUser.value?.id))

    function onSelectUser(row: UserItem) {
      selectedUser.value = row
    }

    function onCreateUserClick() {
      userForm.value = { userName: '', password: '', roles: [] }
      userDialogVisible.value = true
    }

    function onEditUserClick() {
      if (!selectedUser.value) return
      userForm.value = {
        userName: selectedUser.value.userName,
        password: '',
        roles: [...selectedUser.value.roles],
      }
      userDialogVisible.value = true
    }

    function onUserDialogClose() {
      userDialogVisible.value = false
      userForm.value = { userName: '', password: '', roles: [] }
    }

    async function onSaveUserClick() {
      if (!userForm.value.userName.trim()) {
        ElMessage.error(t('security.users.validation.usernameRequired'))
        return
      }

      savingUser.value = true
      try {
        if (isUserCreateMode.value) {
          if (!userForm.value.password) {
            ElMessage.error(t('security.users.validation.passwordRequired'))
            savingUser.value = false
            return
          }
          await store.createUser({
            userName: userForm.value.userName.trim(),
            password: userForm.value.password,
            roles: [...userForm.value.roles],
          })
          ElMessage.success(t('security.users.feedback.created'))
        } else if (selectedUser.value) {
          await store.updateUser(selectedUser.value.id, {
            userName: userForm.value.userName.trim(),
            roles: [...userForm.value.roles],
          })
          ElMessage.success(t('security.users.feedback.updated'))
          await store.loadUsers()
          selectedUser.value = store.users.find((u) => u.id === selectedUser.value?.id) || null
        }
        userDialogVisible.value = false
      } catch (error) {
        const msg = error instanceof Error ? error.message : t('security.common.error')
        ElMessage.error(msg)
      } finally {
        savingUser.value = false
      }
    }

    async function onDeleteUserClick() {
      if (!selectedUser.value?.id) return
      try {
        await ElMessageBox.confirm(
          t('security.users.feedback.confirmDelete'),
          t('security.common.warning'),
          { type: 'warning', confirmButtonText: t('security.common.delete'), cancelButtonText: t('security.common.cancel') }
        )
        await store.deleteUser(selectedUser.value.id)
        ElMessage.success(t('security.users.feedback.deleted'))
        selectedUser.value = null
      } catch (error) {
        if (error !== 'cancel') {
          const msg = error instanceof Error ? error.message : t('security.common.error')
          ElMessage.error(msg)
        }
      }
    }

    function onManageRolesClick() {
      rolesDialogVisible.value = true
    }

    function onRolesDialogClose() {
      rolesDialogVisible.value = false
    }

    async function onSaveRolesClick() {
      if (!selectedUser.value) return
      savingRoles.value = true
      try {
        const checkedKeys = rolesTreeRef.value?.getCheckedKeys() || []
        const newRoles = checkedKeys.filter((key: string) => key !== 'roles-group')
        await store.updateUser(selectedUser.value.id, {
          userName: selectedUser.value.userName,
          roles: newRoles,
        })
        selectedUser.value.roles = newRoles
        ElMessage.success(t('security.users.feedback.rolesUpdated'))
        rolesDialogVisible.value = false
      } catch (error) {
        const msg = error instanceof Error ? error.message : t('security.common.error')
        ElMessage.error(msg)
      } finally {
        savingRoles.value = false
      }
    }

    function isRoleAssigned(roleId: string): boolean {
      return selectedUser.value?.roles.includes(roleId) || false
    }

    onMounted(async () => {
      try {
        await store.loadAll()
      } catch (error) {
        const msg = error instanceof Error ? error.message : t('security.common.error')
        ElMessage.error(msg)
      }
    })

    return {
      t,
      store,
      selectedUser,
      userDialogVisible,
      rolesDialogVisible,
      savingUser,
      savingRoles,
      rolesTreeRef,
      userForm,
      isUserCreateMode,
      userDialogTitle,
      userRolesData,
      rolesTreeData,
      canEdit,
      onSelectUser,
      onCreateUserClick,
      onEditUserClick,
      onUserDialogClose,
      onSaveUserClick,
      onDeleteUserClick,
      onManageRolesClick,
      onRolesDialogClose,
      onSaveRolesClick,
      isRoleAssigned,
    }
  },
})
