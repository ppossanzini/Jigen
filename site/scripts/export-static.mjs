import { copyFile, mkdir, writeFile } from "node:fs/promises";

const workerUrl = new URL("../dist/server/index.js", import.meta.url);
workerUrl.searchParams.set("export", Date.now().toString());
const { default: worker } = await import(workerUrl.href);

const repositoryName = process.env.GITHUB_REPOSITORY?.split("/").pop();
const basePath = process.env.GITHUB_ACTIONS && repositoryName ? `/${repositoryName}` : "";
const routes = ["", "architecture", "embedded", "server", "clients", "benchmarks"];

for (const route of routes) {
  const response = await worker.fetch(
    new Request(`https://jigen.local/${route}`, { headers: { accept: "text/html" } }),
    { ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) } },
    { waitUntil() {}, passThroughOnException() {} },
  );
  if (!response.ok) throw new Error(`Static export failed for /${route} with ${response.status}`);
  const html = (await response.text())
    .replace(/(["'])\/(?!\/)(?=_next\/|favicon\.svg|architecture\/|embedded\/|server\/|clients\/|benchmarks\/)/g, `$1${basePath}/`)
    .replace(/href="\/"/g, `href="${basePath}/"`);
  const target = route
    ? new URL(`../dist/client/${route}/index.html`, import.meta.url)
    : new URL("../dist/client/index.html", import.meta.url);
  if (route) await mkdir(new URL(`../dist/client/${route}/`, import.meta.url), { recursive: true });
  await writeFile(target, html);
}
await copyFile(
  new URL("../dist/client/index.html", import.meta.url),
  new URL("../dist/client/404.html", import.meta.url),
);
await writeFile(new URL("../dist/client/.nojekyll", import.meta.url), "");
