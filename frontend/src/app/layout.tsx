import type { Metadata } from "next";
import { Newsreader, DM_Sans } from "next/font/google";
import "./globals.css";

const newsreader = Newsreader({
  subsets: ["latin"],
  variable: "--font-newsreader",
  display: "swap",
  style: ["normal", "italic"],
});

const dmSans = DM_Sans({
  subsets: ["latin"],
  variable: "--font-dm-sans",
  display: "swap",
  weight: ["400", "500", "600", "700"],
});

export const metadata: Metadata = {
  title: {
    template: "%s | Yusay",
    default: "Yusay — Seguimiento personal del bienestar",
  },
  description:
    "Plataforma de seguimiento personal estructurado del bienestar emocional de forma no clínica, continua y fundamentada.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="es" className={`${newsreader.variable} ${dmSans.variable}`}>
      <body className="min-h-screen bg-canvas text-midnight font-sans antialiased selection:bg-sage/30 selection:text-forest">
        {children}
      </body>
    </html>
  );
}
