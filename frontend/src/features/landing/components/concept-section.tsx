import { Reveal } from "@/components/ui/reveal";

export function ConceptSection() {
  return (
    <section
      id="concepto"
      className="border-y border-frosted-glass/50 bg-warm-stone py-16 lg:py-24"
    >
      <div className="max-w-7xl mx-auto px-6 lg:px-12">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-baseline">
          {/* Manifiesto claro (5 cols) */}
          <Reveal
            delay={0.05}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-5 space-y-4"
          >
            <span className="text-xs font-mono uppercase tracking-widest text-pewter block">
              Perspectiva Temporal
            </span>
            <blockquote className="font-serif text-2xl sm:text-3xl lg:text-[2.25rem] text-forest-depths leading-snug font-light">
              «El bienestar adquiere sentido cuando lo miras con perspectiva de
              tiempo.»
            </blockquote>

            {/* Ilustración de línea continua de cadencia circadiana */}
            <div className="pt-2" aria-hidden="true">
              <svg
                viewBox="0 0 400 36"
                fill="none"
                xmlns="http://www.w3.org/2000/svg"
                className="w-full max-w-sm h-7 text-forest-depths/30"
              >
                <path
                  d="M1 18C40 18 60 4 100 4C140 4 160 32 200 32C240 32 260 4 300 4C340 4 360 18 399 18"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                />
                <circle cx="100" cy="4" r="2.5" fill="currentColor" />
                <circle cx="200" cy="32" r="2.5" fill="currentColor" />
                <circle cx="300" cy="4" r="2.5" fill="currentColor" />
              </svg>
              <div className="flex justify-between max-w-sm text-[10px] font-mono uppercase tracking-widest text-pewter mt-1">
                <span>Día 01</span>
                <span>Oscilación serena</span>
                <span>Semana 04</span>
              </div>
            </div>
          </Reveal>

          {/* Explicación reflexiva (7 cols) */}
          <Reveal
            delay={0.15}
            duration={0.5}
            yOffset={16}
            className="lg:col-span-7 space-y-4 text-pewter text-base sm:text-lg leading-relaxed lg:pl-8 border-t lg:border-t-0 lg:border-l border-frosted-glass pt-6 lg:pt-0 font-normal"
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
            <div className="pt-2">
              <a
                href="#metodo"
                className="inline-flex items-center gap-1.5 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity"
              >
                <span>Conocer nuestro método de 3 pasos</span>
                <span>→</span>
              </a>
            </div>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
