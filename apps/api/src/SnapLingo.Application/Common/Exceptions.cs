namespace SnapLingo.Application.Common;

/// <summary>The request is well-formed but fails a domain/business rule (maps to HTTP 400).</summary>
public class AppValidationException(string message) : Exception(message);

/// <summary>The requested resource does not exist, or does not belong to the caller (maps to HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request conflicts with existing state, e.g. an email that is already registered (maps to HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>The caller is authenticated but has used up an allowance, e.g. the maximum number of lessons (maps to HTTP 403).</summary>
public class LimitExceededException(string message) : Exception(message);

/// <summary>Credentials are missing or invalid (maps to HTTP 401).</summary>
public class AuthenticationException(string message) : Exception(message);
