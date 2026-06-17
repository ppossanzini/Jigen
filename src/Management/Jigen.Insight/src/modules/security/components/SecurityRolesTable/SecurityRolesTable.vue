<template>
  <section class="security-table-card">
    <el-table :data="rows" :empty-text="emptyLabel" :loading="loading" @row-click="onRowClick" height="100%">
      <el-table-column prop="name" :label="nameLabel" min-width="240" />
      <el-table-column prop="id" :label="idLabel" min-width="220" />
      <el-table-column :label="actionsLabel" width="100">
        <template #default="scope">
          <el-button
            class="icon-action"
            :aria-label="`${openLabel} ${scope.row.name}`"
            @click.stop="$emit('open', scope.row)"
          >
            <i class="ti ti-list-details" />
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-row">
      <el-pagination
        layout="prev, pager, next"
        :current-page="currentPage"
        :page-size="pageSize"
        :total="total"
        @current-change="onPageChange"
      />
      <span v-if="hasReliableCount" class="rows-counter">
        {{ perPageLabel }}: {{ visibleRowsCount }}
        <template v-if="total > visibleRowsCount"> / {{ total }}</template>
      </span>
    </div>
  </section>
</template>

<script lang="ts" src="./SecurityRolesTable.ts"></script>
<style scoped lang="less" src="./SecurityRolesTable.less"></style>
