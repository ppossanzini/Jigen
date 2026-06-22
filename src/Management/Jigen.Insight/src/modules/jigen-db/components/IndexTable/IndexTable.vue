<template>
  <section class="index-table-card">
    <el-table :data="rows" @row-click="onRowClick" height="100%">
      <el-table-column prop="name" :label="nameLabel" min-width="260">
        <template #default="scope">
          <div class="index-name-cell">
            <strong>{{ scope.row.name }}</strong>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="collectionsCount" :label="collectionsLabel" width="170" />
      <el-table-column :label="actionsLabel" width="110">
        <template #default="scope">
          <div class="row-actions">
            <el-button
              class="icon-action"
              :aria-label="`${readActionLabel} ${scope.row.name}`"
              @click.stop="$emit('read-collections', scope.row)"
            >
              <i class="ti ti-list-details" />
            </el-button>
            <el-tooltip :disabled="!deleteDisabled" :content="adminOnlyHint" placement="top">
              <el-button
                class="icon-action danger"
                :aria-label="`${deleteActionLabel} ${scope.row.name}`"
                :disabled="deleteDisabled"
                @click.stop="$emit('delete', scope.row)"
              >
                <i class="ti ti-trash" />
              </el-button>
            </el-tooltip>
          </div>
        </template>
      </el-table-column>
    </el-table>

  </section>
</template>

<script lang="ts" src="./IndexTable.ts"></script>
<style scoped lang="less" src="./IndexTable.less"></style>
