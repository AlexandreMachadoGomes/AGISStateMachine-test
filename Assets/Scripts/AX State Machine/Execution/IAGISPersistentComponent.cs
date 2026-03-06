// File: IAGISPersistentComponent.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// This interface has been merged into IAGISPersistentNodeType, which is now constraint-free
// and can be implemented by both node types and MonoBehaviour components.
// This alias is kept so that existing implementing classes compile without modification.

namespace AGIS.ESM.Runtime
{
    public interface IAGISPersistentComponent : IAGISPersistentNodeType { }
}
