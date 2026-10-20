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
    "bg-forest text-canvas hover:bg-forest-hover shadow-sm hover:shadow active:scale-[0.99]",
  secondary:
    "border border-forest/30 text-forest hover:border-forest hover:bg-forest/5 active:scale-[0.99]",
  terracotta:
    "bg-terracotta text-canvas hover:bg-terracotta-hover shadow-sm hover:shadow active:scale-[0.99]",
  ghost:
    "text-midnight hover:text-forest hover:bg-sage-light/50 active:scale-[0.99]",
  "outline-white":
    "border border-canvas/40 text-canvas hover:bg-canvas/10 hover:border-canvas active:scale-[0.99]",
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
