<template>
  <header class="security-toolbar">
    <div class="title-group">
      <h2>{{ title }}</h2>
      <p>{{ subtitle }}</p>
    </div>

    <div class="actions">
      <el-button type="success" @click="$emit('create')">{{ createLabel }}</el-button>
      <el-button @click="$emit('refresh')">{{ refreshLabel }}</el-button>
      <el-button type="danger"  :disabled="deleteDisabled" @click="$emit('delete')">{{ deleteLabel }}</el-button>
    </div>
  </header>
</template>

<script lang="ts" src="./SecurityRolesToolbar.ts"></script>
<style scoped lang="less" src="./SecurityRolesToolbar.less"></style>
