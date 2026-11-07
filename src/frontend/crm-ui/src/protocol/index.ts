// Entry point without React: `import { defineDialog } from "@ogz/crm-ui/protocol"` keeps React out of
// form script bundles.

export {
  createRequest,
  defineDialog,
  encodeRequest,
  MAX_ENCODED_REQUEST_LENGTH,
  readRequest,
  takeResult,
  toErrorMessage,
  writeResult,
  type DialogDefinition,
  type DialogInput,
  type DialogRequest,
  type DialogResult,
  type DialogValue,
} from "./dialog.js";
