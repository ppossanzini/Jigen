<script setup lang="ts">
defineOptions({
  name: 'PagePlaceholder'
});

interface Props {
  /** Page title, shown in the card header */
  title: string;
  /** Short description of what this section will contain */
  desc?: string;
}

withDefaults(defineProps<Props>(), {
  desc: ''
});
</script>

<template>
  <NCard :title="title" :bordered="false" size="large" class="card-wrapper size-full flex-col">
    <div class="size-full min-h-0 flex-1 flex-col-center gap-16px">
      <NEmpty :description="$t('common.lookForward')">
        <template v-if="desc" #extra>
          <p class="text-center text-14px text-gray-500">{{ desc }}</p>
        </template>
      </NEmpty>
    </div>
  </NCard>
</template>
