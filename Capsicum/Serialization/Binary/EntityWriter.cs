using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Capsicum.Serialization.Binary {
    /*
     *  Int32  - FileVersion
     *  Int32  - EntityCount
     * Entity Record:
     *  Byte   - Entity Start         - 0xFA
     *  Int32  - Component count
     * -- This Component sub-record repeats
     * {
     *  Byte   - Component Start      - 0xF1
     *  String - Component Qualified Name
     *  Int32  - Component data length
     *  byte[] - Component data
     *  Byte   - Component End        - 0xF2
     * }
     *  Byte   - Entity End           - 0xFB
     */

    public class EntityWriter {
        public void WriteTo(string outPath, IEnumerable<Entity> entities) {
            using (var writer = new BinaryWriter(new FileStream(outPath, FileMode.CreateNew))) {
                writer.Write(Serialize(entities));

                writer.Flush();
                writer.Close();
            }
        }

        public void WriteTo(string outPath, Entity entity) {
            using (var writer = new BinaryWriter(new FileStream(outPath, FileMode.CreateNew))) {
                writer.Write(Serialize(entity));

                writer.Flush();
                writer.Close();
            }
        }

        public byte[] Serialize(IEnumerable<Entity> entities) {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(Constants.FileVersion);
            bw.Write(entities.Count());

            foreach (var entity in entities) {
                var entityData = Serialize(entity);
                ms.Write(entityData, 0, entityData.Length);
            }

            return ms.ToArray();
        }

        private byte[] Serialize(Entity entity) {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(Constants.EntityStart);
                {
                    var components = entity.GetComponents().OfType<ISerializableComponent>().ToList();

                    writer.Write(components.Count());

                    foreach (var component in components) {
                        writer.Write(Constants.ComponentStart);

                        writer.Write(component.GetType().AssemblyQualifiedName);
                        var buffer = component.Serialize();
                        writer.Write(buffer.Length);
                        writer.Write(buffer);

                        writer.Write(Constants.ComponentEnd);
                    }
                }
                writer.Write(Constants.EntityEnd);
            }

            ms.Flush();
            return ms.ToArray();
        }
    }
}