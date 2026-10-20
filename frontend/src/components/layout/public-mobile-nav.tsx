"use client";

import { useState } from "react";
import Link from "next/link";
import { Menu, X, ArrowRight } from "lucide-react";

interface NavLink {
  href: string;
  label: string;
}

const NAV_LINKS: NavLink[] = [
  { href: "#concepto", label: "Concepto" },
  { href: "#metodo", label: "Cómo funciona" },
  { href: "#herramientas", label: "Herramientas" },
  { href: "#privacidad", label: "Privacidad" },
];

export function PublicMobileNav() {
  const [isOpen, setIsOpen] = useState(false);

  const toggle = () => setIsOpen((prev) => !prev);
  const close = () => setIsOpen(false);

  return (
    <div className="md:hidden">
      <button
        type="button"
        onClick={toggle}
        aria-expanded={isOpen}
        aria-label={isOpen ? "Cerrar menú de navegación" : "Abrir menú de navegación"}
        className="inline-flex items-center justify-center p-2 rounded-xl text-forest hover:bg-sage-light/60 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50"
      >
        {isOpen ? <X className="h-6 w-6" aria-hidden="true" /> : <Menu className="h-6 w-6" aria-hidden="true" />}
      </button>

      {isOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Menú móvil de navegación"
          className="absolute top-20 left-0 right-0 z-50 bg-canvas/98 backdrop-blur-md border-b border-sage/40 shadow-lg px-6 py-6 space-y-4 animate-in fade-in slide-in-from-top-2 duration-200"
        >
          <nav className="flex flex-col space-y-3 font-sans text-sm font-medium text-midnight-muted">
            {NAV_LINKS.map((link) => (
              <a
                key={link.href}
                href={link.href}
                onClick={close}
                className="py-2 px-3 rounded-lg hover:text-forest hover:bg-sage-light/40 transition-colors"
              >
                {link.label}
              </a>
            ))}
          </nav>

          <div className="pt-4 border-t border-sage/30 flex flex-col gap-2.5">
            <Link
              href="/auth/login"
              onClick={close}
              className="w-full inline-flex items-center justify-center px-4 py-2.5 rounded-full border border-forest/30 text-forest text-xs font-semibold uppercase tracking-wider hover:bg-forest/5 transition-colors"
            >
              Iniciar sesión
            </Link>
            <Link
              href="/auth/registro"
              onClick={close}
              className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-full bg-forest text-canvas text-xs font-semibold uppercase tracking-wider hover:bg-forest-hover shadow-sm transition-all"
            >
              <span>Crear cuenta</span>
              <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
