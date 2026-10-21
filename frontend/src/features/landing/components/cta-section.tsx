import Link from "next/link";
import { ArrowRight, Leaf } from "lucide-react";
import { Reveal } from "@/components/ui/reveal";

export function CtaSection() {
  return (
    <section
      id="comenzar"
      className="py-16 lg:py-28 max-w-7xl mx-auto px-6 lg:px-12 w-full text-center"
    >
      <Reveal
        delay={0.1}
        duration={0.5}
        yOffset={16}
        className="max-w-3xl mx-auto bg-warm-stone border border-frosted-glass/60 p-8 sm:p-14 rounded-3xl shadow-none space-y-6"
      >
        <div className="w-14 h-14 rounded-full bg-snow-white mx-auto flex items-center justify-center text-forest-depths border border-frosted-glass shadow-none">
          <Leaf className="h-6 w-6 text-forest-depths" />
        </div>

        <div className="space-y-3">
          <span className="text-xs font-mono uppercase tracking-widest text-pewter">
            Tu propio ritmo
          </span>
          <h2 className="font-serif text-3xl sm:text-4xl text-forest-depths font-light tracking-tight leading-snug">
            Empieza hoy a observar tu bienestar con mayor perspectiva
          </h2>
          <p className="font-sans text-sm sm:text-base text-pewter max-w-lg mx-auto leading-relaxed">
            Crea tu espacio personal y accede a las herramientas de registro
            diario, seguimiento longitudinal y recursos psicoeducativos.
          </p>
        </div>

        <div className="pt-2 flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link
            href="/auth/registro"
            className="inline-flex items-center justify-center gap-2 px-8 py-4 rounded-full bg-forest-depths text-snow-white font-sans text-sm font-medium tracking-normal hover:opacity-90 transition-opacity shadow-none select-none"
          >
            <span>Crear cuenta</span>
            <ArrowRight className="h-4 w-4" />
          </Link>

          <Link
            href="/auth/login"
            className="inline-flex items-center justify-center px-7 py-4 rounded-full border border-forest-depths/30 text-forest-depths font-sans text-sm font-medium hover:border-forest-depths hover:bg-forest-depths/5 transition-colors shadow-none select-none"
          >
            Ya tengo una cuenta
          </Link>
        </div>

        <p className="text-xs text-pewter tracking-wide uppercase pt-2 font-mono">
          Acceso privado · Sin permanencia · Enfoque estrictamente no clínico
        </p>
      </Reveal>
    </section>
  );
}
