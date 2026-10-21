"use client";

import { useState } from "react";
import { motion, useReducedMotion } from "motion/react";
import { Heart, Waves, Zap, Moon, CheckCircle2, MessageSquareText } from "lucide-react";

interface DimensionConfig {
  id: string;
  name: string;
  icon: typeof Heart;
  iconColor: string;
  labels: Record<number, string>;
}

const DIMENSIONS: DimensionConfig[] = [
  {
    id: "animo",
    name: "Estado de Ánimo",
    icon: Heart,
    iconColor: "text-terracotta",
    labels: {
      1: "Nivel 1 de 5 · Muy decaído",
      2: "Nivel 2 de 5 · Desanimado",
      3: "Nivel 3 de 5 · Neutro y estable",
      4: "Nivel 4 de 5 · Favorable y sereno",
      5: "Nivel 5 de 5 · Pleno y optimista",
    },
  },
  {
    id: "estres",
    name: "Nivel de Estrés",
    icon: Waves,
    iconColor: "text-forest",
    labels: {
      1: "Nivel 1 de 5 · Muy bajo / En calma",
      2: "Nivel 2 de 5 · Bajo a moderado",
      3: "Nivel 3 de 5 · Moderado / Manejable",
      4: "Nivel 4 de 5 · Elevado / Tensión alta",
      5: "Nivel 5 de 5 · Muy elevado / Sobrecarga",
    },
  },
  {
    id: "energia",
    name: "Nivel de Energía",
    icon: Zap,
    iconColor: "text-forest",
    labels: {
      1: "Nivel 1 de 5 · Agotamiento",
      2: "Nivel 2 de 5 · Fatiga leve",
      3: "Nivel 3 de 5 · Funcional",
      4: "Nivel 4 de 5 · Buena vitalidad",
      5: "Nivel 5 de 5 · Plena energía",
    },
  },
  {
    id: "sueno",
    name: "Calidad de Sueño",
    icon: Moon,
    iconColor: "text-terracotta",
    labels: {
      1: "Nivel 1 de 5 · Muy deficiente",
      2: "Nivel 2 de 5 · Inquieto / Ligero",
      3: "Nivel 3 de 5 · Aceptable",
      4: "Nivel 4 de 5 · Reparador",
      5: "Nivel 5 de 5 · Profundo y reparador",
    },
  },
];

export function InteractiveCheckInMockup() {
  const shouldReduceMotion = useReducedMotion();
  const [values, setValues] = useState<Record<string, number>>({
    animo: 4,
    estres: 2,
    energia: 4,
    sueno: 4,
  });

  const [hasInteracted, setHasInteracted] = useState(false);
  const [showNote, setShowNote] = useState(false);

  const handleSelect = (dimensionId: string, level: number) => {
    setValues((prev) => ({ ...prev, [dimensionId]: level }));
    setHasInteracted(true);
  };

  return (
    <div className="bg-snow-white/5 border border-snow-white/15 rounded-2xl p-6 sm:p-7 shadow-none relative overflow-hidden transition-colors">
      {/* Cabecera de la Pausa Diaria con estado en tiempo real */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-snow-white/10 pb-4 mb-5">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2 py-0.5 rounded-full border border-snow-white/30 font-mono text-[10px] font-medium text-snow-white uppercase tracking-wider">
              RD-01
            </span>
            <span className="text-[0.6875rem] uppercase tracking-wider text-snow-white/70 font-mono font-medium">
              Registro Diario
            </span>
            <span className="text-[10px] uppercase tracking-wider bg-lime-pulse text-forest-depths font-mono font-medium px-2 py-0.5 rounded-full">
              Pausa de 1 min
            </span>
          </div>
          <h3 className="font-serif text-xl sm:text-2xl text-snow-white font-light mt-0.5 tracking-tight">
            Anotación de hoy
          </h3>
        </div>

        {/* Indicador de persistencia */}
        <div className="flex items-center gap-1.5 self-start sm:self-auto bg-snow-white/10 px-3 py-1.5 rounded-full border border-snow-white/20 text-xs font-mono text-snow-white/90">
          <motion.span
            animate={
              hasInteracted && !shouldReduceMotion
                ? { scale: [1, 1.4, 1] }
                : {}
            }
            transition={{ duration: 0.3 }}
            className={`w-2 h-2 rounded-full ${
              hasInteracted ? "bg-lime-pulse" : "bg-lime-pulse/70 animate-pulse"
            }`}
            aria-hidden="true"
          />
          <span className="text-xs">
            {hasInteracted ? "Anotación actualizada" : "Anotación guardada"}
          </span>
        </div>
      </div>

      <p className="text-xs text-snow-white/75 mb-4 font-sans">
        Interactúa con la escala del <strong>1 al 5</strong> para observar cómo anotas cada aspecto con total sencillez:
      </p>

      {/* Lista de Dimensiones Interactivas */}
      <div className="space-y-3 mb-5">
        {DIMENSIONS.map((dim) => {
          const Icon = dim.icon;
          const currentLevel = values[dim.id];

          return (
            <div
              key={dim.id}
              className="p-3.5 rounded-lg bg-snow-white/5 border border-snow-white/10 transition-colors hover:border-snow-white/20"
            >
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
                {/* Nombre y etiqueta descriptiva dinámica */}
                <div className="flex items-center gap-2.5">
                  <div className="w-7 h-7 rounded-lg bg-snow-white/10 border border-snow-white/20 flex items-center justify-center shrink-0 shadow-none">
                    <Icon className="h-4 w-4 text-snow-white" />
                  </div>
                  <div>
                    <span className="text-[0.6875rem] uppercase tracking-wide text-snow-white/60 block font-mono font-medium">
                      {dim.name}
                    </span>
                    <motion.span
                      key={currentLevel}
                      initial={shouldReduceMotion ? false : { opacity: 0.6, y: -2 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ duration: 0.2 }}
                      className="text-xs text-snow-white font-medium block font-sans"
                    >
                      {dim.labels[currentLevel]}
                    </motion.span>
                  </div>
                </div>

                {/* Selector interactivo de 5 niveles */}
                <div
                  role="radiogroup"
                  aria-label={`Nivel para ${dim.name}`}
                  className="flex items-center gap-1.5 self-end sm:self-auto bg-snow-white/10 p-1 rounded-lg border border-snow-white/15"
                >
                  {[1, 2, 3, 4, 5].map((level) => {
                    const isSelected = level === currentLevel;
                    const isFilled = level <= currentLevel;

                    return (
                      <motion.button
                        key={level}
                        type="button"
                        role="radio"
                        aria-checked={isSelected}
                        aria-label={`Nivel ${level}`}
                        whileHover={shouldReduceMotion ? undefined : { scale: 1.1 }}
                        whileTap={shouldReduceMotion ? undefined : { scale: 0.92 }}
                        onClick={() => handleSelect(dim.id, level)}
                        className={`w-7 h-7 rounded-md text-xs font-mono font-medium flex items-center justify-center transition-all cursor-pointer ${
                          isSelected
                            ? "bg-lime-pulse text-forest-depths shadow-none font-bold"
                            : isFilled
                            ? "bg-snow-white/20 text-snow-white hover:bg-snow-white/30"
                            : "bg-transparent text-snow-white/60 hover:bg-snow-white/10 hover:text-snow-white"
                        }`}
                      >
                        {level}
                      </motion.button>
                    );
                  })}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Sección opcional: Nota reflexiva interactiva */}
      <div className="border-t border-snow-white/10 pt-3">
        <button
          type="button"
          onClick={() => setShowNote(!showNote)}
          className="text-xs text-lime-pulse font-mono inline-flex items-center gap-1.5 hover:underline py-1 group cursor-pointer"
        >
          <MessageSquareText className="h-3.5 w-3.5 text-lime-pulse" />
          <span>{showNote ? "Ocultar nota reflexiva" : "+ Ver nota reflexiva de contexto"}</span>
        </button>

        {showNote && (
          <motion.div
            initial={shouldReduceMotion ? false : { opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            transition={{ duration: 0.3 }}
            className="mt-2.5 p-3.5 rounded-lg bg-snow-white/10 border border-snow-white/20 text-xs text-snow-white leading-relaxed shadow-none"
          >
            <p className="font-serif italic text-snow-white font-light">
              «Paseo tranquilo al atardecer. Menor sobrecarga tras priorizar solo dos tareas principales en la mañana.»
            </p>
            <span className="text-[10px] uppercase tracking-wider text-snow-white/60 block mt-1 font-mono">
              Opcional · Ventana flexible de 7 días
            </span>
          </motion.div>
        )}
      </div>

      {/* Pie de tarjeta informativo */}
      <div className="mt-4 pt-3 border-t border-snow-white/10 flex flex-wrap items-center justify-between gap-2 text-xs text-snow-white/70 font-mono">
        <span className="flex items-center gap-1.5 font-medium text-snow-white">
          <CheckCircle2 className="h-4 w-4 text-lime-pulse" />
          4 aspectos fundamentales
        </span>
        <span className="text-[0.6875rem] text-snow-white/60">
          Escala sencilla del 1 al 5 · Sin juicios
        </span>
      </div>
    </div>
  );
}
