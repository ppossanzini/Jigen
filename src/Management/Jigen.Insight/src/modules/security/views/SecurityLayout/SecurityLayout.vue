<template>
  <div class="security-layout">
    <div class="security-header">
      <h1>{{ t('security.layout.title') }}</h1>
      <p>{{ t('security.layout.description') }}</p>
    </div>
    <div class="security-content">
      <RouterView />
    </div>
  </div>
</template>

<script lang="ts" src="./SecurityLayout.ts"></script>
<style scoped lang="less" src="./SecurityLayout.less"></style>
