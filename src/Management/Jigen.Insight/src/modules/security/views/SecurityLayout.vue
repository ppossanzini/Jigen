<template>
  <section class="module-shell">
    <header class="module-shell__header">
      <h2>{{ t('security.layout.title') }}</h2>
      <p>{{ t('security.layout.description') }}</p>
    </header>

    <div class="module-shell__content">
      <RouterView />
    </div>
  </section>
</template>

<script lang="ts" src="./SecurityLayout.ts"></script>
<style scoped lang="less" src="./SecurityLayout.less"></style>
