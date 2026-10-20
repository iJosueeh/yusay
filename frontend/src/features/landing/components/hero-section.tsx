import Link from "next/link";
import Image from "next/image";
import { ArrowRight, ArrowDown } from "lucide-react";
import heroImg from "@/assets/hero-img.png";
import { Reveal } from "@/components/ui/reveal";

export function HeroSection() {
  return (
    <section className="relative px-6 lg:px-12 pt-10 pb-16 lg:pt-14 lg:pb-20 max-w-7xl mx-auto w-full">
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-12 items-center">
        {/* Narrativa Izquierda (7 cols) */}
        <Reveal
          delay={0.05}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-7 flex flex-col justify-center space-y-6 z-10"
        >
          {/* Cápsula de acento */}
          <div className="inline-flex items-center gap-2.5 px-3 py-1.5 rounded-full bg-sage-subtle border border-sage/40 w-fit">
            <span
              className="w-2 h-2 rounded-full bg-terracotta animate-pulse motion-reduce:animate-none"
              aria-hidden="true"
            />
            <span className="font-sans text-xs uppercase tracking-widest text-midnight-muted font-medium">
              Plataforma no clínica de bienestar personal
            </span>
          </div>

          {/* Titular Expresivo en Newsreader */}
          <h1 className="font-serif text-4xl sm:text-5xl lg:text-[3.75rem] text-forest leading-[1.1] tracking-tight font-normal">
            Un espacio para{" "}
            <span className="italic text-terracotta">comprenderte</span>, a tu
            ritmo.
          </h1>

          {/* Subtítulo */}
          <p className="font-sans text-base sm:text-lg text-midnight-muted max-w-xl leading-relaxed">
            Yusay es una plataforma digital concebida para registrar tu
            bienestar diario, realizar evaluaciones periódicas, observar la
            evolución de tus patrones y acceder a recursos formativos en un
            entorno sereno, seguro y estructurado.
          </p>

          {/* CTAs */}
          <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-4 pt-1">
            <Link
              href="/auth/registro"
              className="inline-flex items-center justify-center gap-2.5 px-7 py-3.5 rounded-full bg-forest text-canvas font-sans text-sm font-semibold tracking-wide hover:bg-forest-hover hover:-translate-y-0.5 hover:shadow-md transition-all duration-300 shadow-sm motion-reduce:hover:transform-none motion-reduce:transition-none"
            >
              <span>Comenzar</span>
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </Link>

            <a
              href="#concepto"
              className="inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-full bg-transparent border border-forest/30 text-forest font-sans text-sm font-medium hover:border-forest hover:bg-forest/5 hover:-translate-y-0.5 transition-all duration-300 motion-reduce:hover:transform-none motion-reduce:transition-none"
            >
              <span>Conocer Yusay</span>
              <ArrowDown className="h-4 w-4 text-terracotta" aria-hidden="true" />
            </a>
          </div>

          {/* Respaldo de sosiego */}
          <div className="pt-2 flex items-center gap-4 text-xs tracking-wider uppercase text-midnight-muted/80">
            <span className="w-8 h-px bg-sage" aria-hidden="true" />
            <span>
              Registro consciente · Evaluaciones estructuradas · Historial y
              recursos
            </span>
          </div>
        </Reveal>

        {/* Espacio Visual Calibrado (5 cols) */}
        <Reveal
          delay={0.15}
          duration={0.5}
          yOffset={16}
          className="lg:col-span-5 relative mt-2 sm:mt-4 lg:mt-0 pb-6 sm:pb-8 lg:pb-0"
        >
          {/* Marco geométrico de acento en salvia desfasado */}
          <div
            className="absolute -inset-2 lg:-inset-3 border border-sage/50 rounded-2xl sm:rounded-3xl transform rotate-1 pointer-events-none z-0"
            aria-hidden="true"
          />

          {/* Tarjeta principal contenedora */}
          <div className="relative z-10 bg-white/95 p-3 sm:p-4 rounded-2xl shadow-sm border border-sage/40 group">
            {/* Contenedor con overflow-hidden EXCLUSIVAMENTE para la imagen */}
            <div className="relative max-h-[460px] sm:max-h-[500px] aspect-[4/5] overflow-hidden rounded-xl bg-sage-subtle">
              <Image
                src={heroImg}
                alt="Momento de contemplación tranquila con una taza de té junto a la ventana"
                priority
                className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-700 ease-out motion-reduce:transform-none motion-reduce:transition-none"
              />
            </div>

            {/* Etiqueta flotante superior derecha: Bienestar Consciente */}
            <div className="absolute top-5 right-5 sm:top-6 sm:right-6 z-20 bg-canvas/92 backdrop-blur-sm px-3 py-1 rounded-full text-forest border border-forest/15 shadow-xs select-none">
              <span className="text-[0.625rem] sm:text-[0.6875rem] uppercase tracking-widest font-semibold font-sans">
                Bienestar Consciente
              </span>
            </div>

            {/* Tarjeta terracota flotante inferior izquierda: Acompañamiento sereno */}
            <div className="absolute -bottom-4 sm:-bottom-5 -left-2 sm:-left-4 lg:-left-5 z-20 bg-terracotta text-white px-4 py-3 sm:px-5 sm:py-3.5 rounded-xl shadow-md max-w-[210px] sm:max-w-[240px] md:max-w-[260px] transition-transform duration-300 group-hover:-translate-y-1 motion-reduce:transform-none motion-reduce:transition-none">
              <span className="text-[0.625rem] sm:text-[0.6875rem] uppercase tracking-widest text-white/85 block font-sans font-semibold mb-0.5">
                Acompañamiento sereno
              </span>
              <p className="font-serif italic text-xs sm:text-sm md:text-base leading-snug">
                «Un espacio estructurado para observar tu bienestar cotidiano sin
                diagnósticos.»
              </p>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
