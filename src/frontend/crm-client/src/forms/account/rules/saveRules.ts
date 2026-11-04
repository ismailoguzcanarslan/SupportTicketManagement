// Account save checks (OnSave).

import { account_preferredcontactmethodcode, accountFields } from "@ogz/crm-entities";
import { RequiredWhenRule, type SaveRule } from "../../../rules/index.js";

export const saveRules: readonly SaveRule[] = [
  // A reminder, not a validation: severity "info" shows it on save without blocking the save.
  RequiredWhenRule({
    field: accountFields.telephone1,
    whenField: accountFields.preferredcontactmethodcode,
    when: account_preferredcontactmethodcode.Phone,
    message: "Phone is the preferred contact method, but no phone number is set.",
    id: "account_phone_recommended",
    severity: "info",
  }),
];
