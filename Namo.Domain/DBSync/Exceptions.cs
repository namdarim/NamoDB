using System;

namespace Namo.Domain.DBSync;

public class DeletedObjectException : Exception
{
    public DeletedObjectException(string message) : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public class IntegrityException : Exception
{
    public IntegrityException(string message) : base(message) { }
}

public class RollbackRejectedException : Exception
{
    public RollbackRejectedException(string message) : base(message) { }
}
