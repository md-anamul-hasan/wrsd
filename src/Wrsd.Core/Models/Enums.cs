namespace Wrsd.Core.Models;

public enum RegistryHiveType
{
    ClassesRoot,
    CurrentUser,
    LocalMachine,
    Users,
    CurrentConfig
}

public enum RegistryValueKind
{
    None = 0,
    String = 1,
    ExpandString = 2,
    Binary = 3,
    DWord = 4,
    MultiString = 7,
    QWord = 11,
    Unknown = 999
}

public enum DiffChangeType
{
    Unchanged = 0,
    Added = 1,
    Deleted = 2,
    Modified = 3
}

public enum SnapshotScopePreset
{
    QuickTriage,
    SoftwareOnly,
    SystemServices,
    PersistenceOnly,
    FullSystem,
    Custom
}
