import { PublicHeader } from "@/components/layout/public-header";
import { PublicFooter } from "@/components/layout/public-footer";

export default function PublicLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-screen flex flex-col bg-canvas text-midnight">
      <PublicHeader />
      <main className="w-full flex-1 pt-20">{children}</main>
      <PublicFooter />
    </div>
  );
}
