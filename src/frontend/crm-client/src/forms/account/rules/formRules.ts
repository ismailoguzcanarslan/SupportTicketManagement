// Account UI rules (OnLoad and the OnChange of the columns they depend on). They only change
// visibility and requirement levels; nothing writes data.
// Adjust the tab/section names to the ones on your Account form; missing ones are ignored.

import { account_accountcategorycode, account_preferredcontactmethodcode, accountFields } from "@ogz/crm-entities";
import { FieldRequiredRule, SectionSpecificVisibilityRule, type FormRule } from "../../../rules/index.js";

export const formRules: readonly FormRule[] = [
  SectionSpecificVisibilityRule({
    tab: "DETAILS_TAB",
    section: "COMPANY_PROFILE",
    field: accountFields.accountcategorycode,
    visibleWhen: account_accountcategorycode.Preferred_Customer,
  }),
  FieldRequiredRule({
    targets: accountFields.telephone1,
    field: accountFields.preferredcontactmethodcode,
    requiredWhen: account_preferredcontactmethodcode.Phone,
    level: "recommended",
  }),
];
