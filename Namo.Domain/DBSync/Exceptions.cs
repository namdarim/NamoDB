namespace Namo.Domain.DBSync;

public class DbSyncException : Exception { public DbSyncException(string m) : base(m) {} }

public class ConflictException : DbSyncException { public ConflictException(string m) : base(m) {} }
public class IntegrityException : DbSyncException { public IntegrityException(string m) : base(m) {} }

public class DeletedObjectException : DbSyncException { public DeletedObjectException(string m) : base(m) {} }
public class NoRemoteVersionException : DbSyncException { public NoRemoteVersionException(string m) : base(m) {} }

public class RollbackRejectedException : ConflictException { public RollbackRejectedException(string m) : base(m) {} }
public class LocalModificationDetectedException : ConflictException { public LocalModificationDetectedException(string m) : base(m) {} }
public class RemoteHeadMismatchException : ConflictException { public RemoteHeadMismatchException(string m) : base(m) {} }
public class BackupAlreadyExistsException : IntegrityException { public BackupAlreadyExistsException(string m) : base(m) {} }
