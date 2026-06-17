<template>
  <header class="database-toolbar">
    <div class="title-group">
      <h2>{{ title }}</h2>
      <p>{{ subtitle }}</p>
    </div>

    <div class="actions">
      <el-tooltip :disabled="!createDisabled" :content="adminOnlyHint" placement="top">
        <el-button type="success" :disabled="createDisabled" @click="$emit('create')">{{ createLabel }}</el-button>
      </el-tooltip>
      <el-button @click="$emit('refresh')">{{ refreshLabel }}</el-button>
      <el-tooltip :disabled="!deleteDisabled" :content="adminOnlyHint" placement="top">
        <el-button type="danger"  :disabled="deleteDisabled" @click="$emit('delete')">{{ deleteLabel }}</el-button>
      </el-tooltip>
    </div>
  </header>
</template>

<script lang="ts" src="./DatabaseToolbar.ts"></script>
<style scoped lang="less" src="./DatabaseToolbar.less"></style>
