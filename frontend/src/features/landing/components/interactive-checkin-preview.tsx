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
    <div className="bg-white border border-sage/40 rounded-2xl p-6 sm:p-7 shadow-sm relative overflow-hidden transition-all duration-300 hover:border-forest/50 hover:shadow-md">
      {/* Cabecera del Check-in con estado en tiempo real */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-sage/20 pb-4 mb-5">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-[0.6875rem] uppercase tracking-wider text-terracotta font-semibold">
              Check-in Diario
            </span>
            <span className="text-[10px] uppercase tracking-widest bg-sage-light text-forest font-semibold px-2 py-0.5 rounded-full">
              Pausa de 1 min
            </span>
          </div>
          <h3 className="font-serif text-xl sm:text-2xl text-forest font-normal mt-0.5">
            Registro de hoy
          </h3>
        </div>

        {/* Indicador de persistencia */}
        <div className="flex items-center gap-1.5 self-start sm:self-auto bg-canvas px-3 py-1.5 rounded-full border border-sage/30 text-xs font-medium text-midnight-muted">
          <motion.span
            animate={
              hasInteracted && !shouldReduceMotion
                ? { scale: [1, 1.4, 1] }
                : {}
            }
            transition={{ duration: 0.3 }}
            className={`w-2 h-2 rounded-full ${
              hasInteracted ? "bg-terracotta" : "bg-forest animate-pulse"
            }`}
            aria-hidden="true"
          />
          <span className="text-xs">
            {hasInteracted ? "Registro actualizado" : "Registro guardado"}
          </span>
        </div>
      </div>

      <p className="text-xs text-midnight-muted mb-4 font-sans">
        Interactúa con la escala del <strong>1 al 5</strong> para observar cómo se registra cada dimensión sin respuestas forzadas:
      </p>

      {/* Lista de Dimensiones Interactivas */}
      <div className="space-y-3 mb-5">
        {DIMENSIONS.map((dim) => {
          const Icon = dim.icon;
          const currentLevel = values[dim.id];

          return (
            <div
              key={dim.id}
              className="p-3.5 rounded-xl bg-canvas border border-sage/30 transition-all hover:border-forest/30 hover:bg-sage-subtle/40"
            >
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
                {/* Nombre y etiqueta descriptiva dinámica */}
                <div className="flex items-center gap-2.5">
                  <div className="w-7 h-7 rounded-lg bg-white border border-sage/30 flex items-center justify-center shrink-0 shadow-2xs">
                    <Icon className={`h-4 w-4 ${dim.iconColor}`} />
                  </div>
                  <div>
                    <span className="text-[0.6875rem] uppercase tracking-wide text-midnight-muted block font-semibold">
                      {dim.name}
                    </span>
                    <motion.span
                      key={currentLevel}
                      initial={shouldReduceMotion ? false : { opacity: 0.6, y: -2 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ duration: 0.2 }}
                      className="text-xs text-forest font-medium block"
                    >
                      {dim.labels[currentLevel]}
                    </motion.span>
                  </div>
                </div>

                {/* Selector interactivo de 5 niveles */}
                <div
                  role="radiogroup"
                  aria-label={`Nivel para ${dim.name}`}
                  className="flex items-center gap-1.5 self-end sm:self-auto bg-white/80 p-1 rounded-lg border border-sage/30"
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
                        className={`w-7 h-7 rounded-md text-xs font-semibold flex items-center justify-center transition-all ${
                          isSelected
                            ? "bg-forest text-canvas shadow-xs font-bold"
                            : isFilled
                            ? "bg-sage-light text-forest hover:bg-sage/40"
                            : "bg-transparent text-midnight-muted hover:bg-sage-light/60 hover:text-midnight"
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
      <div className="border-t border-sage/20 pt-3">
        <button
          type="button"
          onClick={() => setShowNote(!showNote)}
          className="text-xs text-forest font-medium inline-flex items-center gap-1.5 hover:text-forest-hover transition-colors py-1 group"
        >
          <MessageSquareText className="h-3.5 w-3.5 text-terracotta group-hover:scale-110 transition-transform" />
          <span>{showNote ? "Ocultar nota reflexiva" : "+ Ver nota reflexiva de contexto"}</span>
        </button>

        {showNote && (
          <motion.div
            initial={shouldReduceMotion ? false : { opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            transition={{ duration: 0.3 }}
            className="mt-2.5 p-3 rounded-xl bg-sage-subtle border border-sage/30 text-xs text-midnight-muted leading-relaxed"
          >
            <p className="font-serif italic text-midnight">
              «Paseo tranquilo al atardecer. Menor sobrecarga tras priorizar solo dos tareas principales en la mañana.»
            </p>
            <span className="text-[10px] uppercase tracking-wider text-midnight-subtle block mt-1 font-sans">
              Opcional · Ventana de edición de 168 horas
            </span>
          </motion.div>
        )}
      </div>

      {/* Pie de tarjeta informativo */}
      <div className="mt-4 pt-3 border-t border-sage/20 flex flex-wrap items-center justify-between gap-2 text-xs text-midnight-muted">
        <span className="flex items-center gap-1.5 font-medium text-forest">
          <CheckCircle2 className="h-4 w-4 text-forest" />
          4 dimensiones oficiales
        </span>
        <span className="text-[0.6875rem] text-midnight-subtle">
          Escala ordinal 1–5 · Sin juicios
        </span>
      </div>
    </div>
  );
}
