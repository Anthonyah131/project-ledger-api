# Features / Bugs Pendientes

---

## BUG — Verificar que todos los mensajes de excepción usan claves de recurso correctas

**Contexto:**
Se detectó que `GlobalExceptionHandlerMiddleware` responde en el idioma incorrecto cuando se lanza una excepción desde un servicio. La causa raíz era que `UseRequestLocalization` estaba registrado **después** de `UseGlobalExceptionHandler` en `Program.cs`. Los valores de `AsyncLocal` (donde .NET almacena `CultureInfo.CurrentUICulture`) no fluyen hacia atrás en la cadena async — si la localización corre después del handler, el `catch` del handler ve la cultura por defecto del servidor.

**Corregido (2025-03-22):** se movió `UseRequestLocalization` antes de `UseGlobalExceptionHandler` en `Program.cs`.

**Corregido (2026-03-23):** se hizo grep completo de todos los `throw new` en `Services/`. Se encontraron 11 strings libres en español en los siguientes archivos y se reemplazaron por claves resx:

- `Services/User/UserService.cs` → `UserAlreadyDeleted` (clave nueva agregada al resx)
- `Services/Mcp/McpContextService.cs` → `ProjectAccessDenied`, `InvalidDateRange`, `InvalidMonth`
- `Services/Transaction/IncomeService.cs` → `DuplicatePartnerInSplits`, `PartnerNotAssignedToProject`, `CannotMixSplitTypes`
- `Services/Transaction/ExpenseService.cs` → `InvalidCurrencyCode`, `TitleRequired`, `DuplicatePartnerInSplits`, `PartnerNotAssignedToProject`, `CannotMixSplitTypes`

Todos los demás throws ya usaban claves PascalCase correctas. El bug queda completamente cerrado.
