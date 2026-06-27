---
description: 'Routing obbligatorio skill backend per progetti .NET/CQRS'
applyTo:
  - '**/*.cs'
  - '**/*.csproj'
  - '**/*.sln'
  - '**/appsettings*.json'
  - '**/Program.cs'
  - '**/Controllers/**/*.cs'
  - '**/Handlers/**/*.cs'
  - '**/Core/**/*.cs'
---
# Backend Skill Routing

When backend files are involved, enforce this routing:

1. Mandatory backend entrypoint: `td-backend-dev`.
2. Route layer ownership to backend subskills:
   - API layer rules: `be-api-dev`
   - Core DTO/CQRS definitions: `be-core-dev`
   - Handlers, EF, and mapping: `be-handlers-dev`
   - Axon dispatch conventions: `be-axon-dev`
   - Hikyaku dispatch conventions: `be-hikyaku-dev`
   - Legacy migration AxonFlow -> Hikyaku: `axonflow-to-hikyaku-migration`
   - CQRS handler tests: `be-cqrs-handler-testing`
3. Conflict precedence:
   - Architecture boundaries from `td-backend-dev`
   - Then layer-owner subskill rules
   - If overlap persists, apply the stricter rule and document the decision
4. Keep backend orchestrator constraints active (for example: no EF migration generation by the agent).
