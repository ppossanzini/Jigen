<template>
  <section class="users-roles-view">
    <el-card class="hero-card">
      <div class="hero-wrap">
        <div>
          <h1>{{ t('usersRoles.title') }}</h1>
          <p>{{ t('usersRoles.description') }}</p>
        </div>
        <el-tag type="warning" effect="dark">{{ apiBaseUrl }}</el-tag>
      </div>
    </el-card>

    <div class="content-grid">
      <UsersPanel
        :title="t('usersRoles.users.title')"
        :create-label="t('usersRoles.actions.createUser')"
        :username-label="t('usersRoles.users.columns.userName')"
        :password-label="t('usersRoles.users.columns.password')"
        :roles-label="t('usersRoles.users.columns.roles')"
        :actions-label="t('usersRoles.common.columns.actions')"
        :edit-label="t('usersRoles.actions.edit')"
        :delete-label="t('usersRoles.actions.delete')"
        :cancel-label="t('usersRoles.actions.cancel')"
        :save-label="t('usersRoles.actions.save')"
        :create-dialog-title="t('usersRoles.users.dialog.createTitle')"
        :edit-dialog-title="t('usersRoles.users.dialog.editTitle')"
        :loading="store.isLoadingUsers"
        :saving="isSavingUser"
        :users="store.users"
        :roles="store.roles"
        @create="onCreateUser"
        @update="onUpdateUser"
        @delete="onDeleteUser"
      />

      <RolesPanel
        :title="t('usersRoles.roles.title')"
        :create-label="t('usersRoles.actions.createRole')"
        :name-label="t('usersRoles.roles.columns.name')"
        :actions-label="t('usersRoles.common.columns.actions')"
        :edit-label="t('usersRoles.actions.edit')"
        :delete-label="t('usersRoles.actions.delete')"
        :cancel-label="t('usersRoles.actions.cancel')"
        :save-label="t('usersRoles.actions.save')"
        :create-dialog-title="t('usersRoles.roles.dialog.createTitle')"
        :edit-dialog-title="t('usersRoles.roles.dialog.editTitle')"
        :loading="store.isLoadingRoles"
        :saving="isSavingRole"
        :roles="store.roles"
        @create="onCreateRole"
        @update="onUpdateRole"
        @delete="onDeleteRole"
      />
    </div>
  </section>
</template>

<script lang="ts" src="./UsersRolesView.ts"></script>
<style scoped lang="less" src="./UsersRolesView.less"></style>
