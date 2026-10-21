import React from "react";
import Link from "next/link";

export type ButtonVariant = "primary" | "secondary" | "terracotta" | "ghost" | "outline-white";
export type ButtonSize = "sm" | "md" | "lg";

export interface ButtonBaseProps {
  variant?: ButtonVariant;
  size?: ButtonSize;
  className?: string;
  children?: React.ReactNode;
}

const variantStyles: Record<ButtonVariant, string> = {
  primary:
    "bg-forest-depths text-snow-white hover:opacity-90 shadow-none active:scale-[0.99]",
  secondary:
    "border border-forest-depths/30 text-forest-depths hover:border-forest-depths hover:bg-forest-depths/5 active:scale-[0.99] shadow-none",
  terracotta:
    "bg-terracotta text-snow-white hover:bg-terracotta-hover shadow-none active:scale-[0.99]",
  ghost:
    "text-pewter hover:text-forest-depths hover:bg-warm-stone active:scale-[0.99] shadow-none",
  "outline-white":
    "border border-snow-white/40 text-snow-white hover:bg-snow-white/10 hover:border-snow-white active:scale-[0.99] shadow-none",
};

const sizeStyles: Record<ButtonSize, string> = {
  sm: "px-3.5 py-1.5 text-xs tracking-wider uppercase font-medium rounded-full",
  md: "px-5 py-2.5 text-xs uppercase tracking-wider font-semibold rounded-full",
  lg: "px-7 py-3.5 text-sm tracking-wide font-semibold rounded-full",
};

export function getButtonClassName(
  variant: ButtonVariant = "primary",
  size: ButtonSize = "md",
  className = ""
): string {
  return `inline-flex items-center justify-center gap-2 transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-forest/50 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 select-none ${variantStyles[variant]} ${sizeStyles[size]} ${className}`.trim();
}

export type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> &
  ButtonBaseProps & {
    href?: never;
  };

export type ButtonLinkProps = React.AnchorHTMLAttributes<HTMLAnchorElement> &
  ButtonBaseProps & {
    href: string;
  };

export function Button(props: ButtonProps | ButtonLinkProps) {
  const { variant = "primary", size = "md", className = "", children, ...rest } = props;
  const combinedClassName = getButtonClassName(variant, size, className);

  if ("href" in props && typeof props.href === "string") {
    const { href, ...anchorRest } = rest as ButtonLinkProps;
    return (
      <Link href={href} className={combinedClassName} {...anchorRest}>
        {children}
      </Link>
    );
  }

  return (
    <button
      type={(props as ButtonProps).type || "button"}
      className={combinedClassName}
      {...(rest as React.ButtonHTMLAttributes<HTMLButtonElement>)}
    >
      {children}
    </button>
  );
}
