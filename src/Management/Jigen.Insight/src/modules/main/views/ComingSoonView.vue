<template>
  <section class="coming-soon-view">
    <el-card>
      <h2>{{ t('comingSoon.title') }}</h2>
      <p>{{ t('comingSoon.subtitle') }}</p>
      <p class="feature-context">{{ t('comingSoon.activeFeature') }}: {{ featureName }}</p>
      <el-button type="primary" @click="goBack">{{ t('comingSoon.backToDashboard') }}</el-button>
    </el-card>
  </section>
</template>

<script lang="ts" src="./ComingSoonView.ts"></script>
<style scoped lang="less" src="./ComingSoonView.less"></style>
