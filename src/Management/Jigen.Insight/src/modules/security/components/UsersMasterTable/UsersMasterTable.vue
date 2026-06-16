<template>
  <section class="users-master-table">
    <div class="table-header">
      <h3>{{ title }}</h3>
    </div>

    <el-table
      :data="rows"
      stripe
      highlight-current-row
      :empty-text="emptyLabel"
      class="table-body"
      @row-click="onRowClick"
      v-loading="loading"
    >
      <el-table-column prop="userName" :label="userNameLabel" min-width="180" />
    </el-table>

    <div class="table-footer">
      <el-pagination
        :current-page="currentPage"
        :page-size="pageSize"
        :total="total"
        layout="prev, pager, next"
        @current-change="onPageChange"
      />
      <span class="rows-label">{{ rowsLabel }}: {{ pageSize }}</span>
    </div>
  </section>
</template>

<script lang="ts" src="./UsersMasterTable.ts"></script>
<style scoped lang="less" src="./UsersMasterTable.less"></style>
