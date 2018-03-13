using Capsicum.Interfaces;

namespace Capsicum.Serialization {
    public interface ISerializableComponent : IComponent {
        byte[] Serialize();
        void Deserialize(byte[] data);
    }
}