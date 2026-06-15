<template>
  <el-card class="login-card">
    <div class="title-wrap">
      <h1 class="title">{{ title }}</h1>
      <p class="tagline">{{ tagline }}</p>
    </div>

    <el-form label-position="top" @submit.prevent="onSubmitInternal">
      <el-form-item :label="usernameLabel">
        <el-input
          v-model="localUserName"
          :placeholder="usernameLabel"
          autocomplete="username"
          clearable
        />
      </el-form-item>

      <el-form-item :label="passwordLabel">
        <el-input
          v-model="localPassword"
          :placeholder="passwordLabel"
          autocomplete="current-password"
          show-password
          type="password"
        />
      </el-form-item>

      <el-alert v-if="errorText" :title="errorText" type="error" show-icon />

      <el-button class="submit-btn" :loading="loading" type="primary" @click="onSubmitInternal">
        {{ submitLabel }}
      </el-button>
    </el-form>
  </el-card>
</template>

<script lang="ts" src="./LoginCard.ts"></script>
<style scoped lang="less" src="./LoginCard.less"></style>
