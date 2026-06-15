<template>
  <section class="coming-soon">
    <el-card class="coming-soon-card">
      <h1>{{ t('comingSoon.title') }}</h1>
      <p>{{ t('comingSoon.message', { feature: featureLabel }) }}</p>
      <el-button type="primary" class="back-btn" @click="goBackHome">
        {{ t('app.actions.backHome') }}
      </el-button>
    </el-card>
  </section>
</template>

<script lang="ts" src="./ComingSoon.ts"></script>
<style scoped lang="less" src="./ComingSoon.less"></style>
