import Link from "next/link";
import { ArrowRight, Leaf } from "lucide-react";
import { Reveal } from "@/components/ui/reveal";

export function CtaSection() {
  return (
    <section
      id="comenzar"
      className="py-14 lg:py-24 max-w-7xl mx-auto px-6 lg:px-12 w-full text-center"
    >
      <Reveal
        delay={0.1}
        duration={0.5}
        yOffset={16}
        className="max-w-3xl mx-auto bg-white/90 border border-sage/40 p-8 sm:p-14 rounded-3xl shadow-sm space-y-6 hover:border-forest/40 transition-all duration-300"
      >
        <div className="w-14 h-14 rounded-full bg-sage-light mx-auto flex items-center justify-center text-forest group">
          <Leaf className="h-6 w-6 text-forest group-hover:scale-110 transition-transform motion-reduce:transform-none" />
        </div>

        <div className="space-y-3">
          <span className="text-xs font-semibold uppercase tracking-widest text-terracotta">
            Tu propio ritmo
          </span>
          <h2 className="font-serif text-3xl sm:text-4xl text-forest font-normal leading-snug">
            Empieza hoy a observar tu bienestar con mayor perspectiva
          </h2>
          <p className="font-sans text-sm sm:text-base text-midnight-muted max-w-lg mx-auto leading-relaxed">
            Crea tu espacio personal y accede a las herramientas de registro
            diario, seguimiento longitudinal y recursos psicoeducativos.
          </p>
        </div>

        <div className="pt-2 flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link
            href="/auth/registro"
            className="inline-flex items-center justify-center gap-2 px-8 py-3.5 rounded-full bg-forest text-canvas font-sans text-sm font-semibold tracking-wide hover:bg-forest-hover hover:-translate-y-0.5 hover:shadow-md transition-all duration-300 shadow-sm motion-reduce:hover:transform-none motion-reduce:transition-none"
          >
            <span>Crear cuenta</span>
            <ArrowRight className="h-4 w-4" />
          </Link>

          <Link
            href="/auth/login"
            className="inline-flex items-center justify-center px-7 py-3.5 rounded-full border border-forest/30 text-forest font-sans text-sm font-medium hover:bg-forest/5 hover:border-forest hover:-translate-y-0.5 transition-all duration-300 motion-reduce:hover:transform-none motion-reduce:transition-none"
          >
            Ya tengo una cuenta
          </Link>
        </div>

        <p className="text-xs text-midnight-muted/80 tracking-wide uppercase pt-2">
          Acceso privado · Sin permanencia · Enfoque estrictamente no clínico
        </p>
      </Reveal>
    </section>
  );
}
