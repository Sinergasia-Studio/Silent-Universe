/// <summary>
/// IFuseReceiver — interface untuk objek yang bisa menerima fuse (misal FuseBox).
/// Letakkan di Core agar bisa diakses dari InventorySystem tanpa circular dependency.
/// </summary>
public interface IFuseReceiver
{
    bool FuseInstalled { get; }
}