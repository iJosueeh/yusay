import React from "react";
import Link from "next/link";
import Image, { type StaticImageData } from "next/image";
import { ArrowLeft } from "lucide-react";
import toolsImg1 from "@/assets/tools-img-1.png";

export interface AuthLayoutShellProps {
  title?: string;
  subtitle?: string;
  eyebrow?: string;
  quote?: string;
  quoteAuthor?: string;
  image?: StaticImageData;
  imageAlt?: string;
  children: React.ReactNode;
}

export function AuthLayoutShell({
  title,
  subtitle,
  eyebrow = "Acceso a tu espacio",
  quote = "“El bienestar no es una meta que conquistar, sino la cadencia tranquila con la que habitas tus días.”",
  quoteAuthor,
  image = toolsImg1,
  imageAlt = "Mesa de madera serena con cuaderno abierto, taza de té e iluminación matutina suave",
  children,
}: AuthLayoutShellProps) {
  return (
    <div className="min-h-screen bg-snow-white font-sans text-forest-depths antialiased flex flex-col justify-between selection:bg-lime-pulse selection:text-forest-depths relative overflow-x-hidden">
      {/* Encabezado Simplificado */}
      <header className="w-full border-b border-warm-stone bg-snow-white sticky top-0 z-40">
        <div className="max-w-7xl mx-auto px-6 lg:px-12 h-20 flex items-center justify-between relative">
          <div className="flex-1 flex justify-start">
            <Link
              href="/"
              className="group inline-flex items-center gap-2 text-sm text-pewter hover:text-forest-depths transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/30 rounded-full py-1.5 px-3"
            >
              <ArrowLeft className="w-4 h-4 transition-transform group-hover:-translate-x-0.5" />
              <span className="tracking-wide">Volver al inicio</span>
            </Link>
          </div>

          <div className="flex flex-col items-center justify-center text-center">
            <Link
              href="/"
              className="flex flex-col items-center group focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/30 rounded-lg p-1"
            >
              <span className="font-serif text-3xl md:text-4xl tracking-tight text-forest-depths leading-none font-light group-hover:opacity-85 transition-opacity">
                Yusay
              </span>
              <span className="text-[0.625rem] tracking-[0.22em] uppercase text-pewter mt-1 font-medium font-sans">
                Bienestar Personal
              </span>
            </Link>
          </div>

          <div className="flex-1 hidden md:block" aria-hidden="true" />
        </div>
      </header>

      {/* Contenido Principal */}
      <main className="w-full flex-1 flex items-center justify-center py-10 md:py-16 px-6 lg:px-12">
        <div className="w-full max-w-5xl mx-auto">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-14 items-center">
            {/* Columna Izquierda: Fotografía botánica y calma editorial */}
            <div className="hidden lg:flex lg:col-span-6 flex-col items-center">
              <div className="w-full overflow-hidden rounded-2xl border border-warm-stone bg-snow-white shadow-none">
                <Image
                  src={image}
                  alt={imageAlt}
                  priority
                  className="w-full h-80 lg:h-[26rem] object-cover object-top transition duration-700 hover:scale-[1.01]"
                />
              </div>

              {/* Cita reflexiva */}
              <div className="mt-8 max-w-md w-full text-left p-6 rounded-2xl bg-warm-stone relative shadow-none">
                <p className="font-serif italic text-lg md:text-[1.125rem] text-forest-depths leading-relaxed font-light pl-4 border-l-2 border-forest-depths/30">
                  {quote}
                </p>
                {quoteAuthor && (
                  <span className="text-[0.6875rem] uppercase tracking-widest text-pewter block font-sans font-medium pl-4 mt-3">
                    — {quoteAuthor}
                  </span>
                )}
              </div>
            </div>

            {/* Columna Derecha: Tarjeta del formulario (16px border-radius, shadowless) */}
            <div className="w-full max-w-md mx-auto lg:max-w-none lg:col-span-6">
              <div className="bg-snow-white rounded-2xl border border-warm-stone p-8 md:p-10 shadow-none relative">
                {/* Epígrafe y títulos */}
                {title && (
                  <div className="mb-8 text-left">
                    {eyebrow && (
                      <div className="inline-flex items-center gap-1.5 mb-3 bg-lime-pulse text-forest-depths px-3 py-1 rounded-full font-mono text-[0.6875rem] uppercase tracking-wider font-medium">
                        <span>{eyebrow}</span>
                      </div>
                    )}
                    <h1 className="font-serif text-3xl md:text-4xl text-forest-depths tracking-tight font-light mb-2 leading-tight">
                      {title}
                    </h1>
                    {subtitle && (
                      <p className="text-sm text-pewter leading-relaxed font-sans">
                        {subtitle}
                      </p>
                    )}
                  </div>
                )}

                {children}
              </div>
            </div>
          </div>
        </div>
      </main>

      {/* Pie de Página (Footer) */}
      <footer className="w-full border-t border-warm-stone bg-transparent py-8 mt-auto">
        <div className="max-w-5xl mx-auto px-6 text-center space-y-4">
          <div className="flex flex-col sm:flex-row items-center justify-center gap-4 sm:gap-6 text-xs text-pewter font-sans">
            <span>© 2026 Yusay. Todos los derechos reservados.</span>
            <div className="flex items-center gap-4">
              <Link
                href="/#privacidad"
                className="hover:text-forest-depths transition-colors"
              >
                Política de Privacidad
              </Link>
              <span className="text-ash">•</span>
              <span
                className="text-ash cursor-default"
                title="Documento legal en preparación"
              >
                Términos de Servicio
              </span>
            </div>
          </div>
          <p className="max-w-2xl mx-auto text-[0.78125rem] text-pewter italic leading-relaxed font-serif">
            Yusay es un espacio de autoobservación y contemplación personal. No
            sustituye la atención médica o psicológica profesional.
          </p>
        </div>
      </footer>
    </div>
  );
}
