<template>
  <section class="security-view">
    <SecurityUsersToolbar
      :delete-disabled="!selectedUser"
      @create="onOpenCreateUserDialog"
      @refresh="onRefresh"
      @delete="onDeleteUser"
    />

    <div class="workspace-grid">
      <SecurityUsersTable
        :rows="visibleUsers"
        :loading="securityStore.loadingUsers"
        @row-click="onSelectUser"
        @open="onSelectUser"
      />

      <SecurityUsersDetailPanel
        :user="selectedUserDetail"
        :selected-roles="selectedRoles"
        :role-options="roleOptions"
        :title="selectedUserDetail ? `${selectedUserDetail.userName} Details` : t('security.users.detailTitle')"
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
