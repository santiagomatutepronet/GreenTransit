\## 🔒 Regla obligatoria: Registro de pantallas nuevas en PageDefinitions



Cada vez que se cree un nuevo archivo `.razor` con directiva `@page`, se DEBEN completar

estos pasos adicionales para garantizar que la pantalla aparezca automáticamente en el

módulo de Permisos por Pantalla (`/security/page-permissions`):



\### 1. El auto-descubrimiento en startup ya la detectará



El servicio `IPageDiscoveryService` (ejecutado por `DbInitializer` en cada arranque)

escanea por reflexión todos los componentes Blazor con `\[RouteAttribute]` y sincroniza

la tabla `PageDefinitions`. Por tanto, cualquier página nueva se insertará automáticamente

en BD al siguiente restart. \*\*No es necesario escribir un INSERT manual.\*\*



\### 2. Lo que SÍ debe hacer el desarrollador al crear una nueva página



a) \*\*Asignar el `@attribute \[Authorize]` o `\[Authorize(Policy = ...)]` correcto\*\*

&#x20;  según el patrón descrito en `PATRON\_AUTORIZACION\_PAGINAS.md`:

&#x20;  - Patrón A (`\[Authorize]`): todos los autenticados ven la página.

&#x20;  - Patrón B (`\[Authorize(Policy = PolicyConstants.XXX)]`): solo perfiles concretos.

&#x20;  - Patrón C: `\[Authorize]` + `ProfileAuthorizeView` para botones de acción.



b) \*\*Si se necesita una policy nueva\*\* (la página no encaja en ninguna existente):

&#x20;  - Añadir la constante en `Domain/Authorization/PolicyConstants.cs`.

&#x20;  - Registrar la policy en `Program.cs` con los perfiles permitidos.

&#x20;  - Documentar en `PATRON\_AUTORIZACION\_PAGINAS.md` (sección correspondiente al módulo).



c) \*\*Verificar que el namespace y la ruta permiten inferir el módulo correctamente.\*\*

&#x20;  El servicio `PageDiscoveryService.InferModuleName()` usa el namespace y la ruta

&#x20;  para clasificar la página en un módulo. Si la nueva página está en un namespace

&#x20;  o ruta no contemplados, actualizar el método `InferModuleName()` en

&#x20;  `Infrastructure/Services/PageDiscoveryService.cs` añadiendo el nuevo caso.



&#x20;  Módulos actuales y sus criterios de inferencia:

&#x20;  | Namespace contiene / Ruta empieza por         | Módulo asignado          |

&#x20;  |------------------------------------------------|--------------------------|

&#x20;  | `Security` · `/users` · `/profiles` · `/security` | Seguridad               |

&#x20;  | `Reporting` · `/traceability` · `/kpis` · `/documents` | Reporting          |

&#x20;  | `Logistics` · `/logistics/`                    | Dashboards Logísticos    |

&#x20;  | `Sustainability` · `/incidents` · `/dum-zones` · `/emissions` · `/plant-energies` | Sostenibilidad |

&#x20;  | `/entities` · `/ler-codes` · `/residues` · `/treatment-operations` | Configuración |

&#x20;  | `/service-orders` · `/waste-moves` · `/entry-\*` · `/treatment-plants` | Operaciones |

&#x20;  | `/agreements` · `/settlements` · `/market-shares` | Contratos y Liquidaciones |

&#x20;  | `/product-declarations`                        | Declaraciones de Producto |



d) \*\*Proporcionar un `PageName` legible\*\* (opcional pero recomendado).

&#x20;  Si el nombre del componente no es autoexplicativo (ej: `RkWidget.razor`),

&#x20;  actualizar el diccionario en `PageDiscoveryService.HumanizeName()` con la

&#x20;  traducción al español. Si no se hace, el admin podrá renombrarlo manualmente

&#x20;  desde `/security/page-permissions`.



\### 3. Checklist rápido al crear una nueva página



\- \[ ] `@page "/mi-nueva-ruta"` definida

\- \[ ] `@attribute \[Authorize...]` con policy adecuada

\- \[ ] Si policy nueva → añadida en `PolicyConstants.cs` + `Program.cs`

\- \[ ] Namespace coherente con el módulo (ej: `Pages/Security/`, `Pages/Reporting/`)

\- \[ ] Si ruta/namespace no mapea a módulo existente → actualizar `InferModuleName()`

\- \[ ] Si nombre del componente no es descriptivo → actualizar `HumanizeName()`

\- \[ ] Entrada añadida en `NavMenu.razor` en la sección correcta con `AuthorizeView`

\- \[ ] Fila añadida en `PATRON\_AUTORIZACION\_PAGINAS.md` en el módulo correspondiente



\### 4. Resultado esperado



Tras desplegar, el administrador verá la nueva pantalla en `/security/page-permissions`

destacada en amarillo (sin permisos configurados) y podrá asignar los niveles de acceso

(Lectura / Escritura / Ambos) para cada perfil del sistema.

