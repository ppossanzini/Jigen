import type { ReactNode } from "react";
import Link from "next/link";

type DetailShellProps = {
  eyebrow: string;
  title: string;
  intro: string;
  index: string;
  children: ReactNode;
};

export function DetailShell({ eyebrow, title, intro, index, children }: DetailShellProps) {
  return (
    <main>
      <header className="nav wrap detailNav">
        <Link className="brand" href="/" aria-label="Jigen DB home"><span className="brandMark">J</span><span>JIGEN <b>DB</b></span></Link>
        <nav aria-label="Technical sections">
          <a href="/architecture/">Architecture</a>
          <a href="/embedded/">Embedded</a>
          <a href="/server/">Server</a>
          <a href="/benchmarks/">Benchmarks</a>
        </nav>
        <a className="navCta" href="https://github.com/ppossanzini/Jigen">GitHub ↗</a>
      </header>
      <section className="detailHero wrap">
        <div><div className="eyebrow"><span /> {eyebrow}</div><h1>{title}</h1><p>{intro}</p></div>
        <aside><small>TECHNICAL NOTE</small><strong>{index}</strong><span>JIGEN DB / FIELD GUIDE</span></aside>
      </section>
      {children}
      <section className="detailNext wrap">
        <div><span>KEEP EXPLORING</span><h2>Choose the next layer.</h2></div>
        <nav><a href="/architecture/">Architecture <b>→</b></a><a href="/embedded/">Embedded engine <b>→</b></a><a href="/server/">Server topology <b>→</b></a><a href="/benchmarks/">Benchmarks <b>→</b></a></nav>
      </section>
      <footer className="footer wrap"><div className="brand"><span className="brandMark">J</span><span>JIGEN <b>DB</b></span></div><p>A native vector database for the .NET ecosystem.</p><div><Link href="/">Home</Link><a href="https://github.com/ppossanzini/Jigen/tree/main/docs">Docs</a><a href="https://github.com/ppossanzini/Jigen">GitHub</a></div></footer>
    </main>
  );
}

export function CodeBlock({ label, children }: { label: string; children: string }) {
  return <div className="detailCode"><header><span>{label}</span><small>JIGEN / EXAMPLE</small></header><pre><code>{children}</code></pre></div>;
}
