using System.IO;
using Capsicum.Interfaces;
using Capsicum.Serialization;

namespace Capsicum.Test {
    public class Helper {
         
    }

    public class WritableComponent : IComponent, ISerializableComponent {
        public string Name { get; set; }

        public WritableComponent() { }
        public WritableComponent(string name) {
            Name = name;
        }

        /// <inheritdoc />
        public byte[] Serialize() {
            using (var sw = new BinaryWriter(new MemoryStream())) {
                sw.Write(Name);
                return ((MemoryStream) sw.BaseStream).ToArray();
            }
        }

        /// <inheritdoc />
        public void Deserialize(byte[] data) {
            using (var sr = new BinaryReader(new MemoryStream(data))) {
                Name = sr.ReadString();
            }
        }
    }

    public class FakeComponent : IComponent
    {
        public string Name { get; set; }
    }
}