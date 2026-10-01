# Fluent UI 2 Web — vendored files

Este directorio contiene archivos redistribuidos de las librerías oficiales de Microsoft,
vendorizados localmente para no depender de un CDN externo en un panel administrativo
de producción.

- `web-components.min.js` — `@fluentui/web-components@3.1.3`, variante **`web-components-all.min.js`**
  del paquete (renombrada al copiarla), con el set completo de custom elements que expone
  la librería en runtime. Licencia MIT. https://www.npmjs.com/package/@fluentui/web-components
  Nota: pese a estar documentado en el `.d.ts`, esta versión NO registra `<fluent-text-area>`
  en ninguno de los dos bundles (curado ni "-all") — el campo "Valor" del modal de
  Configuraciones se quedó en `<textarea>` nativo por ese motivo. Tampoco confiar en
  `<fluent-dropdown>` (ver nota en admin.css) hasta confirmar que una versión más nueva
  del paquete lo arregla.
- `tokens.css` — generado a partir de `@fluentui/tokens@1.0.0-alpha.24`
  (`webLightTheme` / `webDarkTheme`), convertido a variables CSS bajo `:root` y
  `[data-theme="dark"]`. Licencia MIT.
  https://www.npmjs.com/package/@fluentui/tokens

Copyright (c) Microsoft Corporation. Licencia MIT — ver
https://github.com/microsoft/fluentui/blob/master/LICENSE

Para actualizar: `npm view @fluentui/web-components version` / `npm view @fluentui/tokens version`,
descargar el paquete nuevo y repetir el proceso de extracción (ver historial de este archivo
en git para el script usado).
