import Link from "next/link";
import { Lock } from "lucide-react";

export const metadata = {
  title: "Iniciar sesión",
};

export default function LoginPage() {
  return (
    <div className="bg-white border border-sage/40 rounded-3xl p-8 sm:p-10 shadow-sm space-y-6 text-center">
      <div className="w-12 h-12 rounded-full bg-sage-light mx-auto flex items-center justify-center text-forest">
        <Lock className="h-5 w-5 text-forest" />
      </div>

      <div className="space-y-2">
        <span className="text-xs uppercase tracking-widest text-terracotta font-semibold">
          Fase de Integración
        </span>
        <h1 className="font-serif text-2xl sm:text-3xl text-forest font-normal">
          Acceso a la plataforma
        </h1>
        <p className="font-sans text-sm text-midnight-muted leading-relaxed pt-1">
          El módulo de inicio de sesión seguro (BFF con cookies HttpOnly y token
          JWT de 60 minutos) se conectará en la siguiente fase de desarrollo.
        </p>
      </div>

      <div className="pt-2">
        <Link
          href="/"
          className="inline-flex items-center justify-center w-full px-6 py-3 rounded-full bg-forest text-canvas font-sans text-xs uppercase tracking-wider font-semibold hover:bg-forest-hover transition-colors shadow-sm"
        >
          Volver a la página principal
        </Link>
      </div>

      <p className="text-[11px] text-midnight-muted/70 leading-normal">
        No se requiere introducir credenciales en esta etapa preliminar.
      </p>
    </div>
  );
}
