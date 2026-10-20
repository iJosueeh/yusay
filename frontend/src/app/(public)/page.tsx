import {
  HeroSection,
  ConceptSection,
  MethodSection,
  ToolsPreviewSection,
  FeaturesSection,
  PrivacySection,
  CtaSection,
} from "@/features/landing";

export const metadata = {
  title: "Yusay — Bienestar Personal a través del Tiempo",
  description:
    "Espacio estructurado y sereno para registrar tu bienestar cotidiano, realizar evaluaciones periódicas y observar tus patrones a través del tiempo sin diagnósticos ni exigencias.",
};

export default function LandingPage() {
  return (
    <>
      <HeroSection />
      <ConceptSection />
      <MethodSection />
      <ToolsPreviewSection />
      <FeaturesSection />
      <PrivacySection />
      <CtaSection />
    </>
  );
}
