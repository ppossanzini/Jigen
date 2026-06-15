<template>
  <aside class="detail-panel">
    <h3>{{ title }}</h3>

    <template v-if="row">
      <ul>
        <li><span>ID</span><strong>{{ row.id }}</strong></li>
        <li><span>{{ statusLabel }}</span><strong>{{ row.status }}</strong></li>
        <li><span>{{ sizeLabel }}</span><strong>{{ row.size }}</strong></li>
        <li><span>{{ metricLabel }}</span><strong>{{ row.metric }}</strong></li>
        <li><span>{{ shardsLabel }}</span><strong>{{ row.shardsReplicas }}</strong></li>
        <li><span>{{ updatedLabel }}</span><strong>{{ row.updatedAt }}</strong></li>
      </ul>

      <div class="insight-box">
        <h4>{{ insightsTitle }}</h4>
        <p>{{ row.description }}</p>
      </div>
    </template>

    <p v-else class="empty">{{ emptyLabel }}</p>
  </aside>
</template>

<script lang="ts" src="./IndexDetailPanel.ts"></script>
<style scoped lang="less" src="./IndexDetailPanel.less"></style>
