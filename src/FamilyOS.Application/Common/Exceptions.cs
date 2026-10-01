namespace FamilyOS.Application.Common;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entity, object key) : base($"{entity} '{key}' was not found.") { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You are not allowed to perform this action.")
        : base(message) { }
}

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>Alias used by some handlers; maps to domain rule violations.</summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string property, string error)
        : base(error)
    {
        Errors = new Dictionary<string, string[]> { [property] = new[] { error } };
    }
}
