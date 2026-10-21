import React from "react";
import { AlertTriangle, CheckCircle2, Info } from "lucide-react";

export type FormAlertVariant = "error" | "success" | "info";

export interface FormAlertProps {
  variant?: FormAlertVariant;
  message: string;
  className?: string;
}

export function FormAlert({
  variant = "error",
  message,
  className = "",
}: FormAlertProps) {
  if (!message) return null;

  const styles = {
    error: {
      container:
        "bg-terracotta-light/40 border border-terracotta/40 text-forest-depths",
      icon: AlertTriangle,
      iconColor: "text-terracotta",
    },
    success: {
      container:
        "bg-warm-stone border border-frosted-glass text-forest-depths",
      icon: CheckCircle2,
      iconColor: "text-forest-depths",
    },
    info: {
      container:
        "bg-warm-stone border border-frosted-glass text-forest-depths",
      icon: Info,
      iconColor: "text-forest-depths",
    },
  }[variant];

  const Icon = styles.icon;

  return (
    <div
      role="alert"
      aria-live="polite"
      className={`p-4 rounded-lg transition-colors shadow-none ${styles.container} ${className}`.trim()}
    >
      <div className="flex items-start gap-3">
        <Icon
          className={`w-5 h-5 ${styles.iconColor} shrink-0 mt-0.5`}
          aria-hidden="true"
        />
        <div className="text-xs md:text-sm text-forest-depths leading-relaxed font-sans font-normal text-left">
          {message}
        </div>
      </div>
    </div>
  );
}
