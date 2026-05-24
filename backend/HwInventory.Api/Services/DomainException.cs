namespace HwInventory.Api.Services;

public class DomainException(string message) : Exception(message);

/// <summary>Maps to HTTP 404 / gRPC NOT_FOUND.</summary>
public class NotFoundException(string what) : DomainException(what);

/// <summary>Maps to HTTP 409 / gRPC ALREADY_EXISTS.</summary>
public class ConflictException(string what) : DomainException(what);

/// <summary>Maps to HTTP 400 / gRPC INVALID_ARGUMENT.</summary>
public class ValidationException(string what) : DomainException(what);

/// <summary>Maps to HTTP 422 / gRPC FAILED_PRECONDITION.</summary>
public class PreconditionFailedException(string what) : DomainException(what);
