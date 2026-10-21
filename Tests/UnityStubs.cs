// Permite ejecutar el GA de producción en las comprobaciones de .NET, sin cargar Unity ni la GPU.
namespace UnityEngine
{
    public class MonoBehaviour { }
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeField : System.Attribute { }
}
