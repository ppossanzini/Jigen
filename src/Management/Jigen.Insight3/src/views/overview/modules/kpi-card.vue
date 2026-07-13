<script setup lang="ts">
defineOptions({
  name: 'KpiCard'
});

interface Props {
  /** Tile label (already translated) */
  label: string;
  /** Formatted value */
  value: string;
  /** Iconify icon name */
  icon: string;
}

defineProps<Props>();
</script>

<template>
  <NCard :bordered="false" size="small" class="card-wrapper h-full">
    <div class="flex-y-center gap-12px">
      <div class="flex-center rounded-8px bg-primary/10 p-8px text-icon-large text-primary">
        <SvgIcon :icon="icon" />
      </div>
      <div class="min-w-0 flex-col">
        <span class="truncate text-12px text-gray-500">{{ label }}</span>
        <span class="truncate text-20px font-600">{{ value }}</span>
      </div>
    </div>
  </NCard>
</template>
