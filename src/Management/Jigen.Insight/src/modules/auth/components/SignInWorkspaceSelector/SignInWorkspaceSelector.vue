<template>
  <div class="workspace-selector">
    <div class="workspace-selector__icon">
      <i class="ti ti-stack-2" />
    </div>

    <div class="workspace-selector__content">
      <small>{{ label }}</small>
      <div class="workspace-selector__row">
        <el-select
          :model-value="modelValue"
          class="workspace-selector__select"
          @update:model-value="onChange"
        >
          <el-option
            v-for="workspace in options"
            :key="workspace.id"
            :label="workspace.label"
            :value="workspace.id"
          />
        </el-select>
      </div>
    </div>
  </div>
</template>

<script lang="ts" src="./SignInWorkspaceSelector.ts"></script>
<style scoped lang="less" src="./SignInWorkspaceSelector.less"></style>
