import Image from "next/image";
import { Calendar, ClipboardList, LineChart, BookOpen } from "lucide-react";
import toolsImg2 from "@/assets/tools-img-2.png";
import toolsImg3 from "@/assets/tools-img-3.png";
import { Reveal } from "@/components/ui/reveal";

export function FeaturesSection() {
  return (
    <section className="py-14 lg:py-20 max-w-7xl mx-auto px-6 lg:px-12 w-full">
      {/* Cabecera de sección */}
      <Reveal
        duration={0.5}
        yOffset={16}
        className="max-w-2xl mb-12 space-y-2.5"
      >
        <span className="text-xs font-semibold uppercase tracking-widest text-terracotta">
          Capacidades de Yusay
        </span>
        <h2 className="font-serif text-3xl sm:text-4xl text-forest font-normal">
          Herramientas pensadas para acompañar, nunca para exigir
        </h2>
        <p className="text-base text-midnight-muted leading-relaxed font-sans">
          Cuatro módulos esenciales diseñados para ofrecerte una perspectiva
          clara, fundamentada y respetuosa de tu bienestar personal.
        </p>
      </Reveal>

      {/* Bloque 1: Fichas 01 y 02 + Fotografía Manos y Cuaderno */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 lg:gap-8 items-center mb-10 lg:mb-12">
        {/* Fichas de texto (7 cols) */}
        <Reveal
          delay={0.1}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-7 space-y-5"
        >
          {/* 01 Check-in */}
          <div className="bg-white/90 border border-sage/40 p-6 sm:p-7 rounded-2xl shadow-xs hover:border-forest/50 hover:shadow-md hover:-translate-y-1 transition-all duration-300 group motion-reduce:hover:transform-none">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-baseline gap-3">
                <span className="font-serif italic text-xl text-terracotta group-hover:scale-110 transition-transform motion-reduce:transform-none">
                  01
                </span>
                <h3 className="font-serif text-2xl text-forest font-normal">
                  Check-in de bienestar
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-sage-light flex items-center justify-center text-forest group-hover:bg-forest group-hover:text-canvas transition-colors">
                <Calendar className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-midnight-muted leading-relaxed">
              Un registro ágil de menos de un minuto para notar tu estado diario en
              cuatro dimensiones fundamentales (ánimo, estrés, energía y sueño) en
              escala ordinal 1–5. Dispone de una ventana de edición flexible de hasta 168
              horas (7 días) para registrar a tu propio ritmo.
            </p>
            <div className="pt-4 border-t border-sage/20 mt-4 text-[11px] text-forest font-medium uppercase tracking-wider">
              Pausa de 1 min · Escala 1–5 · Ventana de edición de 168h
            </div>
          </div>

          {/* 02 Evaluaciones */}
          <div className="bg-sage-subtle/50 border border-sage/40 p-6 sm:p-7 rounded-2xl shadow-xs hover:border-forest/50 hover:shadow-md hover:-translate-y-1 transition-all duration-300 group motion-reduce:hover:transform-none">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-baseline gap-3">
                <span className="font-serif italic text-xl text-terracotta group-hover:scale-110 transition-transform motion-reduce:transform-none">
                  02
                </span>
                <h3 className="font-serif text-2xl text-forest font-normal">
                  Evaluaciones personales
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-sage-light flex items-center justify-center text-forest group-hover:bg-forest group-hover:text-canvas transition-colors">
                <ClipboardList className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-midnight-muted leading-relaxed">
              Cuestionarios estandarizados periódicos para explorar aspectos
              específicos de tu bienestar, con autoguardado, versiones
              históricas y baremos de interpretación oficial no clínicos.
            </p>
            <div className="pt-4 border-t border-sage/20 mt-4 text-[11px] text-forest font-medium uppercase tracking-wider">
              Instrumentos validados · Autoguardado · Orientación clara
            </div>
          </div>
        </Reveal>

        {/* Fotografía Cuaderno de Lino (5 cols) con hover mejorado */}
        <Reveal
          delay={0.2}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-5"
        >
          <div className="group rounded-2xl overflow-hidden shadow-sm border border-sage/40 max-h-[340px] sm:max-h-[360px] aspect-[4/3] bg-white transition-all duration-300 hover:shadow-md hover:border-forest/40">
            <Image
              src={toolsImg2}
              alt="Manos abriendo con delicadeza un cuaderno de lino sobre mesa de madera"
              className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-700 ease-out motion-reduce:transform-none motion-reduce:transition-none"
            />
          </div>
        </Reveal>
      </div>

      {/* Bloque 2: Fotografía Persona Escribiendo + Fichas 03 y 04 */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 lg:gap-8 items-center">
        {/* Fotografía Persona Escribiendo (5 cols) con hover mejorado */}
        <Reveal
          delay={0.1}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-5 order-2 lg:order-1"
        >
          <div className="group rounded-2xl overflow-hidden shadow-sm border border-sage/40 max-h-[340px] sm:max-h-[360px] aspect-[4/3] bg-white transition-all duration-300 hover:shadow-md hover:border-forest/40">
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
          {/* 03 Historial y tendencias */}
          <div className="bg-sage-subtle/50 border border-sage/40 p-6 sm:p-7 rounded-2xl shadow-xs hover:border-forest/50 hover:shadow-md hover:-translate-y-1 transition-all duration-300 group motion-reduce:hover:transform-none">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-baseline gap-3">
                <span className="font-serif italic text-xl text-terracotta group-hover:scale-110 transition-transform motion-reduce:transform-none">
                  03
                </span>
                <h3 className="font-serif text-2xl text-forest font-normal">
                  Historial y tendencias
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-sage-light flex items-center justify-center text-forest group-hover:bg-forest group-hover:text-canvas transition-colors">
                <LineChart className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-midnight-muted leading-relaxed">
              Visualización cronológica clara de los registros acumulados para
              observar patrones, frecuencias y medianas ordinales sin promedios
              ficticios ni deducciones de causalidad clínica.
            </p>
            <div className="pt-4 border-t border-sage/20 mt-4 text-[11px] text-forest font-medium uppercase tracking-wider">
              Evolución longitudinal · Mediana ordinal · Sin juicios
            </div>
          </div>

          {/* 04 Orientación y recursos */}
          <div className="bg-white/90 border border-sage/40 p-6 sm:p-7 rounded-2xl shadow-xs hover:border-forest/50 hover:shadow-md hover:-translate-y-1 transition-all duration-300 group motion-reduce:hover:transform-none">
            <div className="flex items-center justify-between mb-2.5">
              <div className="flex items-baseline gap-3">
                <span className="font-serif italic text-xl text-terracotta group-hover:scale-110 transition-transform motion-reduce:transform-none">
                  04
                </span>
                <h3 className="font-serif text-2xl text-forest font-normal">
                  Orientación y recursos
                </h3>
              </div>
              <div className="w-8 h-8 rounded-full bg-sage-light flex items-center justify-center text-forest group-hover:bg-forest group-hover:text-canvas transition-colors">
                <BookOpen className="h-4 w-4" />
              </div>
            </div>
            <p className="font-sans text-sm sm:text-base text-midnight-muted leading-relaxed">
              Biblioteca de lectura con contenidos psicoeducativos clasificados
              por temas y dimensiones, con referencias bibliográficas
              contrastadas para fomentar hábitos de autocuidado basados en
              evidencia.
            </p>
            <div className="pt-4 border-t border-sage/20 mt-4 text-[11px] text-forest font-medium uppercase tracking-wider">
              Artículos formativos · Sin publicidad · Fuentes verificadas
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
