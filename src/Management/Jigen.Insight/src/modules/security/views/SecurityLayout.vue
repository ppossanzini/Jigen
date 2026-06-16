<template>
  <section class="security-layout module-shell">
    <header class="security-header module-shell__header">
      <h2>{{ t('security.layout.title') }}</h2>
      <p>{{ t('security.layout.description') }}</p>
    </header>

    <div class="security-content module-shell__content">
      <RouterView />
    </div>
  </section>
</template>

<script lang="ts" src="./SecurityLayout.ts"></script>
<style scoped lang="less" src="./SecurityLayout.less"></style>
