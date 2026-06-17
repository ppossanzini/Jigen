<template>
  <section class="security-view">
    <SecurityUsersToolbar
      :title="t('security.users.title')"
      :subtitle="t('security.users.subtitle')"
      :create-label="t('security.users.actions.create')"
      :refresh-label="t('security.users.actions.refresh')"
      :delete-label="t('security.users.actions.delete')"
      :delete-disabled="!selectedUser"
      @create="onOpenCreateUserDialog"
      @refresh="onRefresh"
      @delete="onDeleteUser"
    />

    <div class="workspace-grid">
      <SecurityUsersTable
        :rows="visibleUsers"
        :current-page="currentPage"
        :page-size="pageSize"
        :total="securityStore.users.length"
        :loading="securityStore.loadingUsers"
        :user-name-label="t('security.users.columns.userName')"
        :id-label="t('security.users.columns.id')"
        :actions-label="t('security.users.actions.actionsLabel')"
        :open-label="t('security.users.actions.openDetail')"
        :per-page-label="t('security.common.rows')"
        :empty-label="t('security.common.empty')"
        @row-click="onSelectUser"
        @open="onSelectUser"
        @page-change="onPageChange"
      />

      <SecurityUsersDetailPanel
        :user="selectedUserDetail"
        :selected-roles="selectedRoles"
        :role-options="roleOptions"
        :title="t('security.users.detailTitle')"
        :id-label="t('security.users.columns.id')"
        :user-name-label="t('security.users.columns.userName')"
        :roles-label="t('security.users.columns.roles')"
        :no-roles-label="t('security.users.noRoles')"
        :choose-label="t('security.users.emptySelection')"
        :save-roles-label="t('security.users.actions.saveRoles')"
        :edit-label="t('security.common.edit')"
        :delete-label="t('security.common.delete')"
        :loading="securityStore.loadingUserDetail"
        :saving="applyingRoles"
        @update:selected-roles="onSelectedRolesChange"
        @save-roles="onSaveRoles"
        @edit="onOpenEditUserDialog"
        @delete="onDeleteUser"
      />
    </div>

    <el-dialog
      :model-value="userDialogVisible"
      :title="userDialogTitle"
      width="560px"
      @close="onCloseUserDialog"
    >
      <el-form label-position="top">
        <el-form-item :label="t('security.users.columns.userName')" required :error="userDialogErrors.userName">
          <el-input v-model="userDialogForm.userName" />
        </el-form-item>

        <el-form-item :label="t('security.users.columns.password')" :error="userDialogErrors.password">
          <el-input v-model="userDialogForm.password" type="password" show-password />
        </el-form-item>

        <el-form-item :label="t('security.users.columns.roles')">
          <el-select v-model="userDialogForm.roles" multiple filterable>
            <el-option v-for="roleName in roleOptions" :key="roleName" :label="roleName" :value="roleName" />
          </el-select>
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="dialog-actions">
          <el-button @click="onCloseUserDialog">{{ t('security.common.cancel') }}</el-button>
          <el-button type="primary" :loading="userDialogSaving" @click="saveDialogUser">
            {{ t('security.common.save') }}
          </el-button>
        </div>
      </template>
    </el-dialog>
  </section>
</template>

<script lang="ts" src="./SecurityUsersView.ts"></script>
<style scoped lang="less" src="./SecurityUsersView.less"></style>
