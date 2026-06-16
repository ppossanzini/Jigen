<template>
  <section class="roles-master-detail">
    <header class="toolbar">
      <h3>{{ t('security.roles.title') }}</h3>
      <el-button type="primary" @click="onOpenCreateRoleDialog">
        {{ t('security.roles.actions.create') }}
      </el-button>
    </header>

    <div class="layout-grid">
      <article class="panel">
        <RolesMasterTable
          :rows="visibleRoles"
          :current-page="currentPage"
          :page-size="pageSize"
          :total="securityStore.roles.length"
          :loading="securityStore.loadingRoles"
          :title="t('security.roles.masterTitle')"
          :role-name-label="t('security.roles.columns.name')"
          :rows-label="t('security.common.rows')"
          :empty-label="t('security.common.empty')"
          @row-click="onSelectRole"
          @page-change="onRolesPageChange"
        />
      </article>

      <article class="panel detail-panel">
        <template v-if="selectedRole">
          <div class="detail-header">
            <h3>{{ t('security.roles.detailTitle') }}</h3>
            <div class="detail-actions">
              <el-button @click="onOpenEditRoleDialog">{{ t('security.common.edit') }}</el-button>
              <el-button type="danger" @click="onDeleteRole">{{ t('security.common.delete') }}</el-button>
            </div>
          </div>

          <el-descriptions :column="1" border>
            <el-descriptions-item :label="t('security.roles.columns.id')">
              {{ selectedRole.id }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('security.roles.columns.name')">
              {{ selectedRole.name ?? '-' }}
            </el-descriptions-item>
          </el-descriptions>

          <h4 class="cross-ref-title">{{ t('security.roles.usersTitle') }}</h4>
          <el-table :data="usersForSelectedRole" :empty-text="t('security.roles.noUsers')">
            <el-table-column prop="userName" :label="t('security.users.columns.userName')" min-width="180" />
          </el-table>
        </template>

        <el-empty v-else :description="t('security.roles.emptySelection')" />
      </article>
    </div>

    <el-dialog
      :model-value="roleDialogVisible"
      :title="roleDialogTitle"
      width="520px"
      :teleported="false"
      class="security-role-dialog"
      @close="onCloseRoleDialog"
    >
      <el-form label-position="top">
        <el-form-item :label="t('security.roles.columns.name')" required>
          <el-input v-model="roleDialogForm.name" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="onCloseRoleDialog">{{ t('security.common.cancel') }}</el-button>
        <el-button type="primary" :loading="roleDialogSaving" @click="saveRole">
          {{ t('security.common.save') }}
        </el-button>
      </template>
    </el-dialog>
  </section>
</template>

<script lang="ts" src="./RolesMasterDetail.ts"></script>
<style scoped lang="less" src="./RolesMasterDetail.less"></style>
