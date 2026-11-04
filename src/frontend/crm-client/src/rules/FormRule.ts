// Declarative form rules: "show/require/disable X when column Y has value Z".

/**
 * When a rule applies, given the value of the column it depends on:
 * a value (strict equality), a list of values (any of them), or a predicate.
 */
export type RuleCondition<T> = T | readonly T[] | ((value: T | null) => boolean);

export interface FormRule {
  /** Columns whose OnChange re-evaluates the rule. */
  readonly dependsOn: readonly string[];

  /** Applies the rule to the form's current state. Must be safe to call any number of times. */
  apply(formContext: Xrm.FormContext): void;
}

/** Condition for "no value": null, empty text or an empty lookup/multi-select. */
export const isEmpty = (value: unknown): boolean =>
  value === null || value === undefined || value === "" || (Array.isArray(value) && value.length === 0);

/** Condition for "has a value"; the opposite of {@link isEmpty}. */
export const hasValue = (value: unknown): boolean => !isEmpty(value);

export function matches<T>(value: T | null, condition: RuleCondition<T>): boolean {
  if (typeof condition === "function") return (condition as (value: T | null) => boolean)(value);
  if (Array.isArray(condition)) return (condition as readonly T[]).includes(value as T);
  return value === condition;
}

export function toList(names: string | readonly string[]): readonly string[] {
  return typeof names === "string" ? [names] : names;
}
