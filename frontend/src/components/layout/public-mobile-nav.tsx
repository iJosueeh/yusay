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
        className="inline-flex items-center justify-center p-2 rounded-lg text-forest-depths hover:bg-warm-stone transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/50"
      >
        {isOpen ? <X className="h-6 w-6" aria-hidden="true" /> : <Menu className="h-6 w-6" aria-hidden="true" />}
      </button>

      {isOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Menú móvil de navegación"
          className="absolute top-full left-0 right-0 z-50 bg-snow-white border-b border-warm-stone shadow-none px-6 py-6 space-y-4 animate-in fade-in slide-in-from-top-2 duration-200"
        >
          <nav className="flex flex-col space-y-3 font-sans text-sm font-normal text-pewter">
            {NAV_LINKS.map((link) => (
              <a
                key={link.href}
                href={link.href}
                onClick={close}
                className="py-2 px-3 rounded-lg hover:text-forest-depths hover:bg-warm-stone transition-colors"
              >
                {link.label}
              </a>
            ))}
          </nav>

          <div className="pt-4 border-t border-warm-stone flex flex-col gap-2.5">
            <Link
              href="/auth/login"
              onClick={close}
              className="w-full inline-flex items-center justify-center px-4 py-3 rounded-full border border-forest-depths/30 text-forest-depths text-xs font-medium uppercase tracking-wider hover:bg-forest-depths/5 transition-colors"
            >
              Iniciar sesión
            </Link>
            <Link
              href="/auth/registro"
              onClick={close}
              className="w-full inline-flex items-center justify-center gap-2 px-4 py-3 rounded-full bg-forest-depths text-snow-white text-xs font-medium uppercase tracking-wider hover:opacity-90 shadow-none transition-all"
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
