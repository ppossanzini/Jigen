<template>
  <div ref="wrapperRef" class="graph-viewer-3d">
    <canvas ref="canvasRef"></canvas>
    <div v-if="hoverNode" class="graph-tooltip" :style="tooltipStyle">
      <div>{{ t('graphExplorer.tooltip.key') }}: {{ hoverNode.keyLabel }}</div>
      <div>{{ t('graphExplorer.tooltip.layer') }}: {{ hoverNode.maxLevel }}</div>
      <div>{{ t('graphExplorer.tooltip.degree') }}: {{ hoverNode.degree }}</div>
      <div v-if="hoverNode.isDeleted">{{ t('graphExplorer.tooltip.deleted') }}</div>
      <div v-if="hoverNode.isEntrypoint">{{ t('graphExplorer.tooltip.entrypoint') }}</div>
    </div>
  </div>
</template>

<script lang="ts" src="./GraphViewer3D.ts"></script>
<style scoped lang="less" src="./GraphViewer3D.less"></style>
