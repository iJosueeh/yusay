import { Reveal } from "@/components/ui/reveal";

export function ConceptSection() {
  return (
    <section
      id="concepto"
      className="border-y border-sage/40 bg-white/50 py-14 lg:py-20"
    >
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-baseline">
          {/* Manifiesto claro (5 cols) */}
          <Reveal
            delay={0.05}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-5 space-y-3"
          >
            <span className="text-xs font-semibold uppercase tracking-widest text-terracotta block">
              Perspectiva Temporal
            </span>
            <blockquote className="font-serif text-2xl sm:text-3xl lg:text-[2.25rem] text-forest leading-snug font-normal">
              «El bienestar adquiere sentido cuando lo miras con perspectiva de
              tiempo.»
            </blockquote>
          </Reveal>

          {/* Explicación reflexiva (7 cols) */}
          <Reveal
            delay={0.15}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-7 space-y-4 text-midnight-muted text-base sm:text-lg leading-relaxed lg:pl-8 border-t lg:border-t-0 lg:border-l border-sage/30 pt-6 lg:pt-0"
          >
            <p>
              Un solo día difícil no define tu equilibrio, del mismo modo que una
              jornada serena no se mantiene por inercia. Observar cómo
              evolucionan tus sensaciones a lo largo de las semanas te permite
              reconocer tus ritmos propios, advertir señales de fatiga antes de
              que se acumulen y entender qué condiciones te devuelven la calma.
            </p>
            <p>
              Yusay te ayuda a hilar esos registros cotidianos para darles
              contexto, convirtiendo observaciones dispersas en una mirada
              global, respetuosa y comprensiva de tu recorrido personal.
            </p>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
