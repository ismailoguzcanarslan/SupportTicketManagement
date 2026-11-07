// Subgrid operations, by the subgrid control's name (set in the form designer).

function gridControl(formContext: Xrm.FormContext, name: string): Xrm.Controls.GridControl | null {
  const control = formContext.getControl<Xrm.Controls.GridControl>(name);
  return control !== null && typeof control.refresh === "function" && typeof control.getGrid === "function" ? control : null;
}

export const Grid = {
  get(formContext: Xrm.FormContext, name: string): Xrm.Controls.GridControl | null {
    return gridControl(formContext, name);
  },

  /** Reloads the subgrid, e.g. after code created related records. Returns false when it is not on the form. */
  refresh(formContext: Xrm.FormContext, name: string): boolean {
    const grid = gridControl(formContext, name);
    grid?.refresh();
    return grid !== null;
  },

  /** Number of records the subgrid shows (all pages); null when it is not on the form. */
  getRecordCount(formContext: Xrm.FormContext, name: string): number | null {
    return gridControl(formContext, name)?.getGrid().getTotalRecordCount() ?? null;
  },
} as const;
