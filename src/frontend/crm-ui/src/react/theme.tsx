// Fluent UI 2 theme of every page. webLightTheme is the theme of the model-driven app itself, so buttons,
// colors, spacing and type match the form the dialog opens over. Change the brand here, once, e.g. with
// createLightTheme(brandRamp) from @fluentui/react-components.

import { FluentProvider, webLightTheme, type Theme } from "@fluentui/react-components";
import type { ReactNode } from "react";

export const ogzTheme: Theme = webLightTheme;

export function OgzThemeProvider({ children }: { readonly children: ReactNode }) {
  return (
    <FluentProvider theme={ogzTheme} style={{ height: "100%" }}>
      {children}
    </FluentProvider>
  );
}
