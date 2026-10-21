import React, { forwardRef } from "react";
import { AlertCircle } from "lucide-react";

export interface FormInputProps
  extends React.InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  helperText?: string;
  icon?: React.ComponentType<{ className?: string }>;
}

export const FormInput = forwardRef<HTMLInputElement, FormInputProps>(
  ({ label, error, helperText, icon: Icon, id, className = "", ...props }, ref) => {
    const inputId = id || props.name;
    const errorId = inputId ? `${inputId}-error` : undefined;
    const helperId = inputId ? `${inputId}-helper` : undefined;

    return (
      <div className="w-full text-left">
        <label
          htmlFor={inputId}
          className="block text-xs font-medium uppercase tracking-wider text-pewter mb-2 font-sans"
        >
          {label}
        </label>

        <div className="relative group">
          {Icon && (
            <div className="absolute left-3.5 top-1/2 -translate-y-1/2 text-pewter/80 group-focus-within:text-forest-depths transition-colors pointer-events-none">
              <Icon className="w-4 h-4" />
            </div>
          )}

          <input
            ref={ref}
            id={inputId}
            aria-invalid={Boolean(error)}
            aria-describedby={
              error ? errorId : helperText ? helperId : undefined
            }
            className={`w-full bg-snow-white border text-forest-depths placeholder:text-pewter/60 text-sm rounded-lg py-3.5 pr-4 transition-all duration-200 shadow-none ${
              Icon ? "pl-10" : "px-4"
            } ${
              error
                ? "border-terracotta focus:border-terracotta focus:ring-1 focus:ring-terracotta/30"
                : "border-frosted-glass hover:border-ash focus:outline-none focus:border-forest-depths focus:ring-1 focus:ring-forest-depths/30"
            } ${className}`.trim()}
            {...props}
          />
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

FormInput.displayName = "FormInput";
