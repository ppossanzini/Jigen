---
description: 'Routing obbligatorio skill frontend Vue/Element Plus'
applyTo:
  - '**/*.vue'
  - '**/*.ts'
  - '**/*.less'
  - '**/src/modules/**'
  - '**/src/components/**'
  - '**/src/services/**'
---
# Frontend Skill Routing

When frontend files are involved, enforce this routing:

1. Mandatory frontend entrypoint: `td-frontend-dev`.
2. Always apply FE baseline: `fe-base-rules`.
3. Route specialized areas to FE subskills:
   - Project/module setup and structure: `fe-project-create-verify`
   - REST service layer and axios patterns: `fe-base-rest-service`
   - Reusable UI component generation: `fe-vue-ui-component-generator`
   - ArcGIS/ESRI integration: `fe-vue-esri-map-integration`
4. If accessibility QA is requested, run `td-accessibility-check` before delivery.
5. If conflicts exist, apply the stricter rule and document the decision.
6. In this project, avoid `el-skeleton`; use alternative loading states (for example `el-empty`, `el-loading`, or conditional content with i18n text).
