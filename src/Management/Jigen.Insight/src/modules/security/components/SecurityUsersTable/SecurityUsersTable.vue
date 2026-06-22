<template>
  <section class="security-table-card">
    <el-table :data="rows" :empty-text="$t('security.common.empty')" :loading="loading" @row-click="onRowClick" height="100%">
      <el-table-column prop="userName" :label="$t('security.users.columns.userName')" min-width="220" />
      <el-table-column prop="id" :label="$t('security.users.columns.id')" min-width="220" />
      <el-table-column :label="$t('security.users.actions.actionsLabel')" width="100">
        <template #default="scope">
          <el-button
            class="icon-action"
            :aria-label="`${$t('security.users.actions.openDetail')} ${scope.row.userName}`"
            @click.stop="$emit('open', scope.row)"
          >
            <i class="ti ti-list-details" />
          </el-button>
        </template>
      </el-table-column>
    </el-table>

  </section>
</template>

<script lang="ts" src="./SecurityUsersTable.ts"></script>
<style scoped lang="less" src="./SecurityUsersTable.less"></style>
