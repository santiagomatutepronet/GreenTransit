namespace GreenTransit.Domain.Authorization;

/// <summary>
/// Tipos de permiso que puede tener un perfil sobre una pantalla o recurso.
/// Se usa como metadato en la matriz de autorización; no se persiste en BD.
/// </summary>
public enum PermissionType
{
    /// <summary>Sin acceso.</summary>
    None,

    /// <summary>Solo lectura (todos los datos del tenant).</summary>
    Read,

    /// <summary>Solo lectura filtrada por datos propios.</summary>
    ReadOwn,

    /// <summary>Crear y leer (todos los datos del tenant).</summary>
    Create,

    /// <summary>Crear y leer filtrado por datos propios.</summary>
    CreateOwn,

    /// <summary>Actualizar registros del tenant.</summary>
    Update,

    /// <summary>Actualizar solo los registros propios asignados.</summary>
    UpdateOwn,

    /// <summary>Eliminar registros del tenant.</summary>
    Delete,

    /// <summary>CRUD completo sobre todos los datos del tenant.</summary>
    FullCrud,

    /// <summary>CRUD completo filtrado por datos propios.</summary>
    FullCrudOwn,

    /// <summary>Crear y leer (sin editar ni eliminar).</summary>
    CreateAndRead,

    /// <summary>Validar (aprobar/rechazar) sin crear ni eliminar.</summary>
    Validate,
}
