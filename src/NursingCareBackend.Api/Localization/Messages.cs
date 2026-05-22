namespace NursingCareBackend.Api.Localization;

/// <summary>
/// Centralised Spanish error and system message strings.
/// Use <see cref="Get"/> to retrieve a value by key; falls back to the key itself when not found.
/// </summary>
public static class Messages
{
    private static readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Authentication / session
        ["errors.no_autorizado"] = "No autorizado",
        ["errors.sesion_sin_usuario"] = "La sesión actual no incluye un identificador de usuario válido.",
        ["errors.sesion_sin_admin"] = "La sesión actual no incluye un identificador administrativo válido.",
        ["errors.inicio_sesion_fallido"] = "Inicio de sesión fallido",
        ["errors.actualizacion_sesion_fallida"] = "Actualización de sesión fallida",
        ["errors.solicitud_invalida"] = "Solicitud inválida",
        ["errors.usuario_id_formato_invalido"] = "El identificador del usuario no tiene un formato válido.",
        ["errors.demasiadas_solicitudes"] = "Demasiadas solicitudes",
        ["errors.rate_limit_login"] = "Has excedido temporalmente los intentos de inicio de sesión. Intenta de nuevo en unos minutos.",
        ["errors.rate_limit_forgot_password"] = "Has excedido temporalmente las solicitudes de recuperación. Intenta de nuevo más tarde.",
        ["errors.rate_limit_reset_password"] = "Has excedido temporalmente los intentos de restablecimiento. Intenta de nuevo más tarde.",
        ["errors.rate_limit_recalculo"] = "Límite de solicitudes excedido",
        ["errors.error_restablecer_contrasena"] = "Error al restablecer contraseña",
        ["errors.google_oauth_no_configurado"] = "Google OAuth no está configurado",

        // Payroll periods
        ["errors.periodo_no_encontrado"] = "Período no encontrado",
        ["errors.rango_fechas_invalido"] = "Rango de fechas inválido",
        ["errors.rango_fechas_detalle"] = "La fecha de fin debe ser igual o posterior a la fecha de inicio.",
        ["errors.datos_invalidos"] = "Datos inválidos",
        ["errors.periodo_cerrado"] = "Período cerrado",
        ["errors.periodo_cerrado_no_modificable"] = "Un período cerrado no se puede modificar ni eliminar.",
        ["errors.periodo_en_uso"] = "Período en uso",
        ["errors.periodo_en_uso_detalle"] = "No se puede modificar ni eliminar: el período ya tiene nómina calculada o deducciones asociadas.",
        ["errors.periodo_solapado"] = "Período solapado",
        ["errors.periodo_solapado_detalle"] = "El período se solapa con un período de nómina existente. Ajusta las fechas para que no coincidan con otro período.",
        ["errors.periodo_vacio"] = "Período sin nómina",
        ["errors.periodo_vacio_detalle"] = "No se puede cerrar un período sin valores. Recalcula la nómina o agrega deducciones antes de cerrarlo.",
        ["errors.fechas_periodo_detalle"] = "El corte debe ser igual o posterior al inicio, y el pago igual o posterior al corte.",

        // Deductions
        ["errors.deduccion_no_encontrada"] = "Deducción no encontrada",

        // Adjustments
        ["errors.ajuste_no_encontrado"] = "Ajuste no encontrado",

        // Overrides
        ["errors.override_no_encontrado"] = "Override no encontrado",

        // Vouchers
        ["errors.periodo_enfermera_no_encontrado"] = "Período o enfermera no encontrado",
        ["errors.periodo_enfermera_no_encontrado_detalle"] = "No se encontraron datos de nómina para el período y la enfermera especificados.",
        ["errors.periodo_zip_no_encontrado"] = "Período no encontrado",
        ["errors.periodo_zip_no_encontrado_detalle"] = "No se encontraron datos de nómina para el período especificado.",
        ["errors.detalle_no_encontrado"] = "Detalle no encontrado",

        // Users
        ["errors.usuario_no_encontrado"] = "Usuario no encontrado",

        // Clients
        ["errors.cliente_no_encontrado"] = "Cliente no encontrado",
        ["errors.cliente_no_encontrado_detalle"] = "No se encontró el cliente solicitado.",
        ["errors.cliente_invalido"] = "Cliente inválido",
        ["errors.cliente_invalido_detalle"] = "Debes seleccionar un cliente activo y válido para crear la solicitud.",
        ["errors.cliente_requerido"] = "Cliente requerido",
        ["errors.cliente_requerido_detalle"] = "Selecciona el cliente para el cual se crea la solicitud.",
        ["errors.cliente_no_valido"] = "Cliente no válido",
        ["errors.cliente_no_valido_detalle"] = "El usuario seleccionado no es un cliente válido.",
        ["errors.acceso_denegado"] = "Acceso denegado",
        ["errors.acceso_denegado_detalle"] = "No tienes permiso para realizar esta acción.",

        // Care requests
        ["errors.solicitud_no_encontrada"] = "Solicitud no encontrada",
        ["errors.solicitud_no_encontrada_detalle"] = "No se encontró la solicitud.",
        ["errors.solicitud_cuidado_no_encontrada_detalle"] = "No se encontró la solicitud de cuidado.",
        ["errors.solicitud_admin_no_encontrada_detalle"] = "No se encontró la solicitud administrativa.",
        ["errors.transicion_invalida"] = "Transición inválida",
        ["errors.estado_invalido"] = "Estado inválido",
        ["errors.recibo_no_encontrado"] = "Recibo no encontrado",
        ["errors.verificacion_no_disponible"] = "Verificación no disponible",
        ["errors.verificacion_no_disponible_detalle"] = "La verificación de precios no está disponible para esta solicitud.",

        // Reports
        ["errors.reporte_no_encontrado"] = "Reporte no encontrado.",
        ["errors.error_reporte"] = "Error al procesar el reporte",
        ["errors.error_reporte_detalle"] = "Ocurrió un error al procesar la solicitud.",
        ["errors.error_exportar_reporte"] = "Error al exportar el reporte",

        // Shifts
        ["errors.turno_no_encontrado"] = "Turno no encontrado",

        // Compensation rules
        ["errors.regla_no_encontrada"] = "Regla no encontrada",

        // Nurse payroll
        ["errors.sin_identidad"] = "Sin identidad",
        ["errors.sin_identidad_detalle"] = "No se pudo determinar el usuario enfermera.",

        // Catalog
        ["errors.solicitud_invalida_punto"] = "Solicitud inválida.",
        ["errors.calculo_invalido"] = "Cálculo inválido.",

        // Messages
        ["messages.rol_asignado"] = "El rol se asignó correctamente.",
        ["messages.admin_creado"] = "El usuario administrador se creó correctamente. Guarda el token y deshabilita /setup-admin en producción.",
        ["messages.cuenta_activada"] = "La cuenta se activó correctamente.",
        ["messages.correo_recuperacion"] = "Si el correo está registrado, se ha enviado un código de recuperación.",
    };

    /// <summary>
    /// Returns the localised string for <paramref name="key"/>.
    /// Falls back to the key itself if no entry is registered.
    /// </summary>
    public static string Get(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;
}
