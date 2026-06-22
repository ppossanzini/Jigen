<template>
  <section class="security-table-card">
    <el-table :data="rows" :empty-text="$t('security.common.empty')" :loading="loading" @row-click="onRowClick" height="100%">
      <el-table-column prop="name" :label="$t('security.roles.columns.name')" min-width="240" />
      <el-table-column prop="id" :label="$t('security.roles.columns.id')" min-width="220" />
      <el-table-column :label="$t('security.roles.actions.actionsLabel')" width="100">
        <template #default="scope">
          <el-button
            class="icon-action"
            :aria-label="`${$t('security.roles.actions.openDetail')} ${scope.row.name}`"
            @click.stop="$emit('open', scope.row)"
          >
            <i class="ti ti-list-details" />
          </el-button>
        </template>
      </el-table-column>
    </el-table>

  </section>
</template>

<script lang="ts" src="./SecurityRolesTable.ts"></script>
<style scoped lang="less" src="./SecurityRolesTable.less"></style>
