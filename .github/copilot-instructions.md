# GitHub Copilot – Instrucciones de Proyecto

Estas instrucciones se aplican a **todas** las conversaciones de GitHub Copilot
dentro de este repositorio.

---

## 1. Rol general de Copilot

Actúas como **asistente de desarrollo agente‑based** para esta solución.
Tu función principal es:
- Generar código alineado con la arquitectura definida
- Respetar estrictamente las fuentes de verdad del proyecto
- Detectar contradicciones, lagunas o ambigüedades antes de implementar

No asumas nada que no esté documentado.

---

## 2. Fuentes de verdad (lectura obligatoria)

Antes de **crear, modificar o proponer** código, debes consultar
los siguientes documentos Markdown:

1. `docs/README.md`
2. `docs/PATRON_AUTORIZACION_PAGINAS.md`
3. `docs/Mapa_Funcionalidades_GreenTransit.md`
4. `docs/Mapa_Autorizacion_GreenTransit.md`
5. `docs/Modelo_de_Datos.md`
6. `docs/COPILOT_CONTEXT.md`
7. `docs/instrucciones_adicionales.md`



Estas fuentes **tienen prioridad absoluta** sobre:
- el historial del chat
- ejemplos genéricos
- convenciones implícitas

Si una petición contradice estos documentos:
- **detén la implementación**
- explica el conflicto
- solicita aclaración o propón una corrección documentada

---

## 3. Arquitectura y capas

La aplicación está organizada por capas.
Copilot debe:

- Respetar responsabilidades por capa
- No introducir dependencias cruzadas no documentadas
- No mezclar lógica de dominio, infraestructura y presentación

Cualquier cambio estructural exige:
- explicación del impacto
- actualización del markdown correspondiente

---

## 4. Reglas de generación de código

- No generes código “de ejemplo” o “demo”
- El código debe ser directamente integrable
- Sigue el stack tecnológico definido en `README.md`
- Si falta información crítica, **decláralo explícitamente**

Evita:
- overengineering
- patrones no documentados
- librerías no aprobadas

---

## 5. Trabajo con agentes

Este proyecto utiliza **agentes personalizados** definidos en:
`.github/agents/`

Cuando se active un agente:
- Aplica sus reglas específicas
- Mantén coherencia con estas instrucciones generales

---

## 6. Documentación viva

Si una implementación introduce:
- nuevas decisiones
- cambios de comportamiento
- nuevas responsabilidades

Debes indicar **qué archivo Markdown debe actualizarse** y cómo.

---

## 7. Principio final

Este repositorio es la única memoria válida del proyecto.
La documentación manda.
El chat no es una fuente de verdad.