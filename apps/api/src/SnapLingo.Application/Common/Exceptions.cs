namespace SnapLingo.Application.Common;

/// <summary>The request is well-formed but fails a domain/business rule (maps to HTTP 400).</summary>
public class AppValidationException(string message) : Exception(message);

/// <summary>The requested resource does not exist, or does not belong to the caller (maps to HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);
