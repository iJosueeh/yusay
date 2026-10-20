import Link from "next/link";
import { AppNav } from "./app-nav";

export function AppHeader() {
  return (
    <header className="sticky top-0 z-30 border-b border-sage/40 bg-canvas/95 backdrop-blur-sm">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        <div className="flex items-center gap-8">
          <Link
            href="/inicio"
            className="flex items-center gap-2 text-2xl font-serif font-bold text-forest tracking-tight transition-opacity hover:opacity-90"
            aria-label="Yusay — Ir a inicio"
          >
            <span className="inline-block text-terracotta select-none" aria-hidden="true">🌿</span>
            <span>Yusay</span>
          </Link>
        </div>

        <AppNav />
      </div>
    </header>
  );
}
