<template>
  <el-card class="sign-in-card" shadow="never">
    <div class="sign-in-card__header">
      <h1>{{ title }}</h1>
      <p>{{ subtitle }}</p>
    </div>

    <el-form
      ref="formRef"
      :model="modelValue"
      :rules="rules"
      label-position="top"
      class="sign-in-card__form"
    >
      <el-form-item :label="emailLabel" prop="email">
        <el-input
          :model-value="modelValue.email"
          :placeholder="emailPlaceholder"
          @update:model-value="onEmailChange"
        />
      </el-form-item>

      <el-form-item :label="passwordLabel" prop="password">
        <el-input
          :model-value="modelValue.password"
          :placeholder="passwordPlaceholder"
          show-password
          type="password"
          @update:model-value="onPasswordChange"
        />
      </el-form-item>

      <div class="sign-in-card__row">
        <el-checkbox
          :model-value="modelValue.rememberMe"
          @update:model-value="onRememberChange"
        >
          {{ rememberLabel }}
        </el-checkbox>

        <el-button link type="primary" @click="$emit('forgot-password')">
          {{ forgotPasswordLabel }}
        </el-button>
      </div>

      <el-button type="primary" class="sign-in-card__submit" :loading="loading" @click="onSubmit">
        {{ submitLabel }}
      </el-button>

      <!-- <div class="sign-in-card__divider">
        <span>{{ continueWithLabel }}</span>
      </div>

      <div class="sign-in-card__social">
        <el-button @click="$emit('oauth-google')">
          <i class="ti ti-brand-google" />
          {{ googleLabel }}
        </el-button>
        <el-button @click="$emit('oauth-sso')">
          <i class="ti ti-users-group" />
          {{ ssoLabel }}
        </el-button>
      </div> -->
    </el-form>

    <!-- <div class="sign-in-card__footer">
      <div class="sign-in-card__workspace-icon">
        <i class="ti ti-rocket" />
      </div>
      <div class="sign-in-card__workspace-copy">
        <strong>{{ lastWorkspaceLabel }}</strong>
        <small>{{ lastWorkspaceName }} - {{ lastWorkspaceSeen }}</small>
      </div>
      <el-button link type="primary" @click="$emit('quick-demo')">{{ quickDemoLabel }}</el-button>
      <el-tooltip :content="infoTooltip" placement="top">
        <el-icon>
          <i class="ti ti-info-circle" />
        </el-icon>
      </el-tooltip>
    </div> -->
  </el-card>
</template>

<script lang="ts" src="./SignInCard.ts"></script>
<style scoped lang="less" src="./SignInCard.less"></style>
