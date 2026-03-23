# Features / Bugs Pendientes

---

## BUG — Verificar que todos los mensajes de excepción usan claves de recurso correctas

**Contexto:**
Se detectó que `GlobalExceptionHandlerMiddleware` responde en el idioma incorrecto cuando se lanza una excepción desde un servicio. La causa raíz era que `UseRequestLocalization` estaba registrado **después** de `UseGlobalExceptionHandler` en `Program.cs`. Los valores de `AsyncLocal` (donde .NET almacena `CultureInfo.CurrentUICulture`) no fluyen hacia atrás en la cadena async — si la localización corre después del handler, el `catch` del handler ve la cultura por defecto del servidor.

**Corregido (2025-03-22):** se movió `UseRequestLocalization` antes de `UseGlobalExceptionHandler` en `Program.cs`.

**Qué revisar aún:**
Hacer un grep de todos los `throw new` en la capa de servicios y verificar que el string que se pasa como mensaje sea una clave válida en `Resources/Messages.resx` (inglés). Si el string es texto libre en cualquier idioma, el middleware lo devolverá tal cual sin traducir.

```bash
# Buscar throws con texto libre (no una clave PascalCase sin espacios)
grep -rn "throw new.*Exception(\"" Services/ --include="*.cs"
```

Criterio: el mensaje debe ser una clave PascalCase sin espacios (ej. `"ProjectNotFound"`), no una frase (ej. `"Project not found"` o `"Proyecto no encontrado"`).
