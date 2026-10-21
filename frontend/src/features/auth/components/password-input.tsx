"use client";

import React, { useState, forwardRef } from "react";
import Link from "next/link";
import { Eye, EyeOff, AlertCircle, Lock } from "lucide-react";

export interface PasswordInputProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> {
  label: string;
  error?: string;
  helperText?: string;
  forgotPasswordHref?: string;
  cornerHint?: React.ReactNode;
  icon?: React.ComponentType<{ className?: string }> | null;
}

export const PasswordInput = forwardRef<HTMLInputElement, PasswordInputProps>(
  (
    {
      label,
      error,
      helperText,
      forgotPasswordHref,
      cornerHint,
      icon: Icon = Lock,
      id,
      className = "",
      ...props
    },
    ref
  ) => {
    const [showPassword, setShowPassword] = useState(false);
    const inputId = id || props.name;
    const errorId = inputId ? `${inputId}-error` : undefined;
    const helperId = inputId ? `${inputId}-helper` : undefined;

    return (
      <div className="w-full text-left">
        <div className="flex items-center justify-between mb-2">
          <label
            htmlFor={inputId}
            className="block text-xs font-medium uppercase tracking-wider text-pewter font-sans"
          >
            {label}
          </label>

          {forgotPasswordHref && (
            <Link
              href={forgotPasswordHref}
              className="text-xs text-pewter hover:text-forest-depths underline underline-offset-4 decoration-frosted-glass transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-forest-depths/40 rounded"
            >
              ¿Olvidaste tu contraseña?
            </Link>
          )}

          {!forgotPasswordHref && cornerHint && (
            <div className="text-xs text-pewter/80 font-sans">
              {cornerHint}
            </div>
          )}
        </div>

        <div className="relative group">
          {Icon && (
            <div className="absolute left-3.5 top-1/2 -translate-y-1/2 text-pewter/80 group-focus-within:text-forest-depths transition-colors pointer-events-none">
              <Icon className="w-4 h-4" />
            </div>
          )}

          <input
            ref={ref}
            id={inputId}
            type={showPassword ? "text" : "password"}
            aria-invalid={Boolean(error)}
            aria-describedby={
              error ? errorId : helperText ? helperId : undefined
            }
            className={`w-full bg-snow-white border text-forest-depths placeholder:text-pewter/60 text-sm rounded-lg py-3.5 pr-12 transition-all duration-200 shadow-none ${
              Icon ? "pl-10" : "px-4"
            } ${
              error
                ? "border-terracotta focus:border-terracotta focus:ring-1 focus:ring-terracotta/30"
                : "border-frosted-glass hover:border-ash focus:outline-none focus:border-forest-depths focus:ring-1 focus:ring-forest-depths/30"
            } ${className}`.trim()}
            {...props}
          />

          <button
            type="button"
            onClick={() => setShowPassword((prev) => !prev)}
            aria-label={
              showPassword ? "Ocultar contraseña" : "Ver contraseña en texto claro"
            }
            className="absolute right-3.5 top-1/2 -translate-y-1/2 text-pewter hover:text-forest-depths transition-colors p-1 rounded-full focus:outline-none focus-visible:ring-2 focus-visible:ring-forest-depths/30"
          >
            {showPassword ? (
              <EyeOff className="w-4 h-4" aria-hidden="true" />
            ) : (
              <Eye className="w-4 h-4" aria-hidden="true" />
            )}
          </button>
        </div>

        {error && (
          <p
            id={errorId}
            className="text-xs text-terracotta mt-1.5 flex items-center gap-1 font-sans font-medium"
          >
            <AlertCircle className="w-3.5 h-3.5 shrink-0" aria-hidden="true" />
            <span>{error}</span>
          </p>
        )}

        {!error && helperText && (
          <p id={helperId} className="text-xs text-pewter mt-1.5 font-sans">
            {helperText}
          </p>
        )}
      </div>
    );
  }
);

PasswordInput.displayName = "PasswordInput";
