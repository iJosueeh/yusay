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
          {/* Cápsula de acento Lime Pulse */}
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-lime-pulse text-forest-depths font-mono text-xs uppercase tracking-wider font-medium w-fit">
            <span>Plataforma no clínica // Bienestar personal</span>
          </div>

          {/* Titular Expresivo Whisper-Light */}
          <h1 className="font-serif text-4xl sm:text-5xl lg:text-[3.75rem] text-forest-depths leading-[1.1] tracking-tight font-light">
            Un espacio para{" "}
            <span className="italic font-light">comprenderte</span>, a tu
            ritmo.
          </h1>

          {/* Subtítulo */}
          <p className="font-sans text-base sm:text-lg text-pewter max-w-xl leading-relaxed font-normal">
            Yusay es una plataforma digital concebida para registrar tu
            bienestar diario, realizar evaluaciones periódicas, observar la
            evolución de tus patrones y acceder a recursos formativos en un
            entorno sereno, seguro y estructurado.
          </p>

          {/* CTAs */}
          <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-4 pt-1">
            <Link
              href="/auth/registro"
              className="inline-flex items-center justify-center gap-2.5 px-7 py-3.5 rounded-full bg-forest-depths text-snow-white font-sans text-sm font-medium tracking-normal hover:opacity-90 transition-opacity shadow-none select-none"
            >
              <span>Comenzar</span>
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </Link>

            <a
              href="#concepto"
              className="inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-full bg-transparent border border-forest-depths/30 text-forest-depths font-sans text-sm font-medium hover:border-forest-depths hover:bg-forest-depths/5 transition-colors shadow-none select-none"
            >
              <span>Conocer Yusay</span>
              <ArrowDown className="h-4 w-4" aria-hidden="true" />
            </a>
          </div>

          {/* Respaldo de sosiego */}
          <div className="pt-2 flex items-center gap-4 text-xs tracking-wider uppercase text-pewter font-mono">
            <span className="w-8 h-px bg-frosted-glass" aria-hidden="true" />
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
          {/* Tarjeta principal contenedora (16px border-radius, shadowless) */}
          <div className="relative z-10 bg-snow-white p-3 sm:p-4 rounded-2xl border border-warm-stone shadow-none group">
            {/* Contenedor con overflow-hidden EXCLUSIVAMENTE para la imagen */}
            <div className="relative max-h-[460px] sm:max-h-[500px] aspect-[4/5] overflow-hidden rounded-xl bg-warm-stone">
              <Image
                src={heroImg}
                alt="Momento de contemplación tranquila con una taza de té junto a la ventana"
                priority
                className="w-full h-full object-cover object-center group-hover:scale-105 transition-transform duration-700 ease-out motion-reduce:transform-none motion-reduce:transition-none"
              />
            </div>

            {/* Etiqueta flotante superior derecha: Specimen Pill */}
            <div className="absolute top-5 right-5 sm:top-6 sm:right-6 z-20 bg-snow-white/95 backdrop-blur-sm px-3 py-1 rounded-full text-forest-depths border border-frosted-glass font-mono text-[0.6875rem] uppercase tracking-wider select-none">
              <span>Bienestar Consciente</span>
            </div>

            {/* Tarjeta flotante inferior izquierda: Warm Stone card, shadowless */}
            <div className="absolute -bottom-4 sm:-bottom-5 -left-2 sm:-left-4 lg:-left-5 z-20 bg-warm-stone text-forest-depths px-5 py-3.5 rounded-2xl border border-frosted-glass shadow-none max-w-[230px] sm:max-w-[260px]">
              <span className="text-[0.625rem] sm:text-[0.6875rem] uppercase tracking-widest text-pewter block font-mono font-medium mb-1">
                Acompañamiento sereno
              </span>
              <p className="font-serif italic text-xs sm:text-sm text-forest-depths font-light leading-snug">
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
