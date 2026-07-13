import * as echarts from 'echarts/core';
import { GraphChart } from 'echarts/charts';
import { Scatter3DChart } from 'echarts-gl/charts';
import { Grid3DComponent } from 'echarts-gl/components';

/**
 * Registers the chart types the Graph Explorer needs on top of the base set already registered by
 * `hooks/common/echarts.ts` (`echarts/core` keeps one global renderer registry, so this only needs
 * to run once — importing this module for its side effect is enough).
 *
 * - `GraphChart` (2D): renders the HNSW graph with server-provided node positions (`layout: 'none'`).
 * - `Scatter3DChart` / `Grid3DComponent` (echarts-gl): 3D projection view (nodes + a sampled-point
 *   approximation of edges — see the comment above `EDGE_SAMPLES` in `graph-3d-chart.vue` for why
 *   `Lines3DChart` isn't used: its layout stage doesn't support the `cartesian3D` coordinate system).
 */
echarts.use([GraphChart, Scatter3DChart, Grid3DComponent]);

export { echarts };
