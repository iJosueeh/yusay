import Link from "next/link";
import Image from "next/image";
import { Calendar, ClipboardList, LineChart, BookOpen } from "lucide-react";
import toolsImg2 from "@/assets/tools-img-2.png";
import toolsImg3 from "@/assets/tools-img-3.png";
import { Reveal } from "@/components/ui/reveal";

export function FeaturesSection() {
  return (
    <section className="py-16 lg:py-24 max-w-7xl mx-auto px-6 lg:px-12 w-full">
      {/* Cabecera de sección */}
      <Reveal
        duration={0.5}
        yOffset={16}
        className="max-w-2xl mb-12 space-y-2.5"
      >
        <span className="text-xs font-mono uppercase tracking-widest text-pewter">
          Capacidades de Yusay
        </span>
        <h2 className="font-serif text-3xl sm:text-4xl text-forest-depths font-light tracking-tight">
          Herramientas pensadas para acompañar, nunca para exigir
        </h2>
        <p className="text-base text-pewter leading-relaxed font-sans">
          Cuatro módulos esenciales diseñados para ofrecerte una perspectiva
          clara, fundamentada y respetuosa de tu bienestar personal.
        </p>
      </Reveal>

      {/* Bloque 1: Fichas CK-01 y EV-02 + Fotografía Manos y Cuaderno */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 lg:gap-8 items-center mb-10 lg:mb-12">
        {/* Fichas de texto (7 cols) */}
        <Reveal
          delay={0.1}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-7 space-y-5"
        >
          {/* RD-01 Registro Diario */}
          <div className="bg-snow-white border border-warm-stone p-6 sm:p-7 rounded-2xl shadow-none hover:border-frosted-glass transition-colors group">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-center gap-3">
                <span className="px-2.5 py-0.5 rounded-full border border-forest-depths/30 font-mono text-[11px] font-medium text-forest-depths uppercase tracking-wider">
                  RD-01
                </span>
                <h3 className="font-serif text-2xl text-forest-depths font-light tracking-tight">
                  Registro diario de bienestar
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-warm-stone flex items-center justify-center text-forest-depths">
                <Calendar className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-pewter leading-relaxed">
              Un registro ágil de menos de un minuto para notar cómo te sientes en
              cuatro aspectos esenciales (ánimo, estrés, energía y descanso) en una
              escala sencilla del 1 al 5. Dispone de una ventana flexible de hasta 7
              días (168 horas) para registrar a tu propio ritmo.
            </p>
            <div className="pt-4 border-t border-warm-stone mt-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
              <span className="text-[11px] text-pewter font-mono uppercase tracking-wider">
                Pausa de 1 min · Escala 1 al 5 · Ventana de 7 días
              </span>
              <a
                href="#herramientas"
                className="inline-flex items-center gap-1.5 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity self-start sm:self-auto"
              >
                <span>Probar simulador diario</span>
                <span>→</span>
              </a>
            </div>
          </div>

          {/* EV-02 Evaluaciones */}
          <div className="bg-snow-white border border-warm-stone p-6 sm:p-7 rounded-2xl shadow-none hover:border-frosted-glass transition-colors group">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-center gap-3">
                <span className="px-2.5 py-0.5 rounded-full border border-forest-depths/30 font-mono text-[11px] font-medium text-forest-depths uppercase tracking-wider">
                  EV-02
                </span>
                <h3 className="font-serif text-2xl text-forest-depths font-light tracking-tight">
                  Evaluaciones personales
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-warm-stone flex items-center justify-center text-forest-depths">
                <ClipboardList className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-pewter leading-relaxed">
              Cuestionarios estandarizados periódicos para explorar aspectos
              específicos de tu bienestar, con autoguardado, versiones
              históricas y baremos de interpretación oficial no clínicos.
            </p>
            <div className="pt-4 border-t border-warm-stone mt-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
              <span className="text-[11px] text-pewter font-mono uppercase tracking-wider">
                Instrumentos validados · Autoguardado
              </span>
              <a
                href="#metodo"
                className="inline-flex items-center gap-1.5 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity self-start sm:self-auto"
              >
                <span>Ver metodología</span>
                <span>→</span>
              </a>
            </div>
          </div>
        </Reveal>

        {/* Fotografía Cuaderno de Lino (5 cols) */}
        <Reveal
          delay={0.2}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-5"
        >
          <div className="group rounded-2xl overflow-hidden shadow-none border border-warm-stone max-h-[340px] sm:max-h-[360px] aspect-[4/3] bg-snow-white">
            <Image
              src={toolsImg2}
              alt="Manos abriendo con delicadeza un cuaderno de lino sobre mesa de madera"
              className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-700 ease-out motion-reduce:transform-none motion-reduce:transition-none"
            />
          </div>
        </Reveal>
      </div>

      {/* Bloque 2: Fotografía Persona Escribiendo + Fichas TR-03 y RC-04 */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 lg:gap-8 items-center">
        {/* Fotografía Persona Escribiendo (5 cols) */}
        <Reveal
          delay={0.1}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-5 order-2 lg:order-1"
        >
          <div className="group rounded-2xl overflow-hidden shadow-none border border-warm-stone max-h-[340px] sm:max-h-[360px] aspect-[4/3] bg-snow-white">
            <Image
              src={toolsImg3}
              alt="Persona anotando sus reflexiones con calma bajo luz natural"
              className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-700 ease-out motion-reduce:transform-none motion-reduce:transition-none"
            />
          </div>
        </Reveal>

        {/* Fichas de texto (7 cols) */}
        <Reveal
          delay={0.2}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-7 space-y-5 order-1 lg:order-2"
        >
          {/* TR-03 Historial y tendencias */}
          <div className="bg-snow-white border border-warm-stone p-6 sm:p-7 rounded-2xl shadow-none hover:border-frosted-glass transition-colors group">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-center gap-3">
                <span className="px-2.5 py-0.5 rounded-full border border-forest-depths/30 font-mono text-[11px] font-medium text-forest-depths uppercase tracking-wider">
                  TR-03
                </span>
                <h3 className="font-serif text-2xl text-forest-depths font-light tracking-tight">
                  Historial y tendencias
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-warm-stone flex items-center justify-center text-forest-depths">
                <LineChart className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-pewter leading-relaxed">
              Visualización cronológica clara de los registros acumulados para
              observar patrones, frecuencias y medianas ordinales sin promedios
              ficticios ni deducciones de causalidad clínica.
            </p>
            <div className="pt-4 border-t border-warm-stone mt-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
              <span className="text-[11px] text-pewter font-mono uppercase tracking-wider">
                Evolución longitudinal · Mediana ordinal
              </span>
              <a
                href="#herramientas"
                className="inline-flex items-center gap-1.5 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity self-start sm:self-auto"
              >
                <span>Ver historial</span>
                <span>→</span>
              </a>
            </div>
          </div>

          {/* RC-04 Orientación y recursos */}
          <div className="bg-snow-white border border-warm-stone p-6 sm:p-7 rounded-2xl shadow-none hover:border-frosted-glass transition-colors group">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-center gap-3">
                <span className="px-2.5 py-0.5 rounded-full border border-forest-depths/30 font-mono text-[11px] font-medium text-forest-depths uppercase tracking-wider">
                  RC-04
                </span>
                <h3 className="font-serif text-2xl text-forest-depths font-light tracking-tight">
                  Orientación y recursos
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-warm-stone flex items-center justify-center text-forest-depths">
                <BookOpen className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-pewter leading-relaxed">
              Biblioteca de lectura con contenidos psicoeducativos clasificados
              por temas y dimensiones, con referencias bibliográficas
              contrastadas para fomentar hábitos de autocuidado basados en
              evidencia.
            </p>
            <div className="pt-4 border-t border-warm-stone mt-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
              <span className="text-[11px] text-pewter font-mono uppercase tracking-wider">
                Artículos formativos · Fuentes verificadas
              </span>
              <Link
                href="/auth/registro"
                className="inline-flex items-center gap-1.5 text-xs font-mono uppercase tracking-wider text-forest-depths border-b-[1.5px] border-forest-depths pb-0.5 hover:opacity-75 transition-opacity self-start sm:self-auto"
              >
                <span>Acceder a la biblioteca</span>
                <span>→</span>
              </Link>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
