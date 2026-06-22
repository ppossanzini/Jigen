<template>
  <article class="metric-card">
    <header>
      <h3>{{ title }}</h3>
      <el-tag :type="tagType" effect="dark">{{ hint }}</el-tag>
    </header>
    <p class="value">{{ value }}</p>
  </article>
</template>

<script lang="ts" src="./DashboardMetricCard.ts"></script>
<style scoped lang="less" src="./DashboardMetricCard.less"></style>
