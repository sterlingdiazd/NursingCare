namespace NursingCareBackend.Api.ErrorHandling;

public static class UserFacingMessageTranslator
{
    private static readonly IReadOnlyDictionary<string, string> ExactMessages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name is required."] = "El nombre es obligatorio.",
            ["Name must contain letters and spaces only."] = "El nombre solo acepta letras y espacios.",
            ["Last name is required."] = "El apellido es obligatorio.",
            ["Last name must contain letters and spaces only."] = "El apellido solo acepta letras y espacios.",
            ["Identification number is required."] = "La cedula es obligatoria.",
            ["Identification number must contain exactly 11 digits."] = "La cedula debe tener exactamente 11 digitos.",
            ["Phone is required."] = "El telefono es obligatorio.",
            ["Phone must contain exactly 10 digits."] = "El telefono debe tener exactamente 10 digitos.",
            ["Email is required."] = "El correo es obligatorio.",
            ["Password is required."] = "La contrasena es obligatoria.",
            ["Passwords do not match."] = "Las contrasenas no coinciden.",
            ["Password must be at least 6 characters long."] = "La contrasena debe tener al menos 6 caracteres.",
            ["User with this email already exists."] = "Ya existe una cuenta con este correo.",
            ["Profile completion is only available for Google-linked users."] =
                "La finalizacion del perfil solo esta disponible para cuentas vinculadas con Google.",
            ["Invalid email or password."] = "Correo o contrasena invalidos.",
            ["User account is not active."] = "La cuenta aun no esta activa.",
            ["Google authorization code is required."] = "El codigo de autorizacion de Google es obligatorio.",
            ["Google account email is not verified."] = "Tu cuenta de Google no tiene el correo verificado.",
            ["Google sign-in is already linked to a different account."] = "Este correo ya esta vinculado a otra cuenta.",
            ["Refresh token is required."] = "El token de actualizacion es obligatorio.",
            ["Refresh token is invalid or expired."] = "El token de actualizacion no es valido o ya vencio.",
            ["Role name is required."] = "El nombre del rol es obligatorio.",
            ["At least one role is required."] = "Debes conservar al menos un rol asignado.",
            ["The selected role is not supported."] = "El rol seleccionado no es compatible con la politica actual.",
            ["The Nurse role can only be assigned to nurse profiles."] =
                "El rol de enfermeria solo puede asignarse a perfiles de enfermeria.",
            ["The Client role can only be assigned to client profiles."] =
                "El rol de cliente solo puede asignarse a perfiles de cliente.",
            ["The Admin role cannot be removed from your own account."] =
                "No puedes quitar el rol administrativo de tu propia cuenta.",
            ["You cannot deactivate your own account."] = "No puedes desactivar tu propia cuenta.",
            ["User account is already active."] = "La cuenta ya esta activa.",
            ["User ID cannot be empty."] = "El identificador del usuario es obligatorio.",
            ["Hire date is required for nurse registration."] =
                "La fecha de contratacion es obligatoria para el registro de enfermeria.",
            ["Specialty is required for nurse registration."] =
                "La especialidad es obligatoria para el registro de enfermeria.",
            ["Bank name is required for nurse registration."] =
                "El banco es obligatorio para el registro de enfermeria.",
            ["Specialty is required."] = "La especialidad es obligatoria.",
            ["Specialty is not valid."] = "La especialidad seleccionada no es valida.",
            ["Bank name is required."] = "El banco es obligatorio.",
            ["Bank name must contain letters and spaces only."] = "El banco solo acepta letras y espacios.",
            ["License ID must contain digits only."] = "La licencia solo acepta numeros.",
            ["Account number must contain digits only."] = "El numero de cuenta solo acepta numeros.",
            ["Category is required."] = "La categoria es obligatoria.",
            ["Category is not valid."] = "La categoria seleccionada no es valida.",
            ["The requested user does not have a nurse profile."] = "El usuario solicitado no tiene un perfil de enfermeria.",
            ["Description cannot be empty."] = "La descripcion es obligatoria.",
            ["CareRequestType is required."] = "El tipo de solicitud es obligatorio.",
            ["Unit must be greater than zero."] = "La cantidad debe ser mayor que cero.",
            ["ClientBasePrice must be > 0 when provided."] =
                "El precio base del cliente debe ser mayor que cero cuando se envia.",
            ["Price must be > 0 when provided."] = "El precio debe ser mayor que cero cuando se envia.",
            ["MedicalSuppliesCost must be >= 0 when provided."] =
                "El costo de insumos medicos no puede ser negativo.",
            ["Calculated total cannot be negative."] = "El total calculado no puede ser negativo.",
            ["Care request must have an assigned nurse before approval."] =
                "La solicitud debe tener una enfermera asignada antes de aprobarse.",
            ["Care request must have an assigned nurse before completion."] =
                "La solicitud debe tener una enfermera asignada antes de completarse.",
            ["Only the assigned nurse can complete this care request."] =
                "Solo la enfermera asignada puede completar esta solicitud.",
            ["Care request cannot be completed before its scheduled care-request date."] =
                "La solicitud no puede completarse antes de la fecha programada.",
            ["Assigned nurse cannot be empty."] = "La enfermera asignada es obligatoria.",
            ["Assigned nurse is required."] = "Debes seleccionar una enfermera.",
            ["Assigned nurse was not found."] = "No se encontro la enfermera seleccionada.",
            ["Assigned user is not a nurse profile."] = "El usuario seleccionado no pertenece a un perfil de enfermeria.",
            ["Assigned nurse must have a completed active profile."] =
                "La enfermera asignada debe tener un perfil activo y completado.",
            ["A valid nurse user identifier is required to complete a care request."] =
                "Se requiere un identificador valido de enfermeria para completar la solicitud.",
            ["Unsupported care request transition."] = "La transicion de la solicitud no es compatible.",
        };

    public static string Translate(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "No fue posible completar la operacion.";
        }

        var normalizedMessage = StripParameterSuffix(message);

        if (ExactMessages.TryGetValue(normalizedMessage, out var translated))
        {
            return translated;
        }

        if (normalizedMessage.StartsWith("User with ID ", StringComparison.Ordinal)
            && normalizedMessage.EndsWith(" not found.", StringComparison.Ordinal))
        {
            return "No se encontro el usuario solicitado.";
        }

        if (normalizedMessage.StartsWith("Care request '", StringComparison.Ordinal)
            && normalizedMessage.EndsWith("' was not found.", StringComparison.Ordinal))
        {
            return "No se encontro la solicitud.";
        }

        if (normalizedMessage.StartsWith("Role '", StringComparison.Ordinal)
            && normalizedMessage.EndsWith("' not found.", StringComparison.Ordinal))
        {
            return "No se encontro el rol solicitado.";
        }

        if (normalizedMessage.EndsWith(" role not found in the system.", StringComparison.Ordinal))
        {
            return "No se encontro el rol requerido en el sistema.";
        }

        if (normalizedMessage.StartsWith("User already has the '", StringComparison.Ordinal)
            && normalizedMessage.EndsWith("' role.", StringComparison.Ordinal))
        {
            return "El usuario ya tiene asignado ese rol.";
        }

        if (normalizedMessage.StartsWith("User with email '", StringComparison.Ordinal)
            && normalizedMessage.EndsWith("' already exists. Use login instead.", StringComparison.Ordinal))
        {
            return "Ya existe una cuenta con este correo. Inicia sesion con esa cuenta.";
        }

        const string pendingStatusPrefix = "Care request can only be ";
        const string pendingStatusMarker = " from Pending status. Current status is ";
        if (normalizedMessage.StartsWith(pendingStatusPrefix, StringComparison.Ordinal)
            && normalizedMessage.Contains(pendingStatusMarker, StringComparison.Ordinal))
        {
            var currentStatus = normalizedMessage[(normalizedMessage.IndexOf(pendingStatusMarker, StringComparison.Ordinal) + pendingStatusMarker.Length)..]
                .TrimEnd('.');

            return $"La solicitud solo puede cambiarse desde el estado Pendiente. Estado actual: {TranslateStatus(currentStatus)}.";
        }

        const string approvedStatusPrefix =
            "Care request can only be completed from Approved status. Current status is ";
        if (normalizedMessage.StartsWith(approvedStatusPrefix, StringComparison.Ordinal))
        {
            var currentStatus = normalizedMessage[approvedStatusPrefix.Length..].TrimEnd('.');
            return $"La solicitud solo puede completarse desde el estado Aprobada. Estado actual: {TranslateStatus(currentStatus)}.";
        }

        if (normalizedMessage.StartsWith("Unknown care_request_type '", StringComparison.Ordinal))
        {
            return "El tipo de solicitud no es valido.";
        }

        if (normalizedMessage.StartsWith("OAuth error:", StringComparison.Ordinal)
            || normalizedMessage.StartsWith("Google token exchange failed:", StringComparison.Ordinal)
            || normalizedMessage.StartsWith("Google user info request failed:", StringComparison.Ordinal))
        {
            return "No se pudo completar la autenticacion con Google.";
        }

        return normalizedMessage;
    }

    private static string TranslateStatus(string status)
    {
        return status switch
        {
            "Pending" => "Pendiente",
            "Approved" => "Aprobada",
            "Rejected" => "Rechazada",
            "Completed" => "Completada",
            _ => status,
        };
    }

    private static string StripParameterSuffix(string message)
    {
        var parameterSuffixIndex = message.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return parameterSuffixIndex >= 0
            ? message[..parameterSuffixIndex]
            : message;
    }
}
