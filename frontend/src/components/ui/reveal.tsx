"use client";

import React from "react";
import { motion, useReducedMotion } from "motion/react";

export interface RevealProps {
  children: React.ReactNode;
  className?: string;
  delay?: number;
  duration?: number;
  yOffset?: number;
}

export function Reveal({
  children,
  className = "",
  delay = 0,
  duration = 0.5,
  yOffset = 16,
}: RevealProps) {
  const shouldReduceMotion = useReducedMotion();

  // Si el usuario prefiere movimiento reducido, mostrar inmediatamente sin desplazamiento
  if (shouldReduceMotion) {
    return <div className={className}>{children}</div>;
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: Math.min(yOffset, 20) }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-30px" }}
      transition={{
        duration: Math.max(0.4, Math.min(duration, 0.6)),
        delay,
        ease: [0.16, 1, 0.3, 1],
      }}
      className={className}
    >
      {children}
    </motion.div>
  );
}
