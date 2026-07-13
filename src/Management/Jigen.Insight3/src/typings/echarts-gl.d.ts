/**
 * `echarts-gl` ships no TypeScript declarations. These modules are only used to side-effect
 * `echarts.use([...])` the 3D scatter/lines/grid installers (see `hooks/common/echarts-graph.ts`),
 * so an `any`-typed ambient module is enough — the actual chart options are typed loosely at the
 * call site since echarts-gl's series/component option shapes aren't part of the core `echarts`
 * type surface either.
 */
declare module 'echarts-gl/charts' {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  export const Scatter3DChart: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  export const Lines3DChart: any;
}

declare module 'echarts-gl/components' {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  export const Grid3DComponent: any;
}
