<template>
  <section class="security-layout">
    <header class="security-header">
      <h2>{{ t('security.layout.title') }}</h2>
      <p>{{ t('security.layout.description') }}</p>
    </header>

    <div class="security-content">
      <RouterView />
    </div>
  </section>
</template>

<script lang="ts" src="./SecurityLayout.ts"></script>
<style scoped lang="less" src="./SecurityLayout.less"></style>
