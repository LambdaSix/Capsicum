using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Capsicum.Interfaces;

namespace Capsicum.Serialization {
    public interface ISerializableComponent : IComponent {
        byte[] Serialize();
        void Deserialize(byte[] data);
    }

    public static class Constants {
        public static int FileVersion = 1;

        public static byte EntityStart = 0xFA;
        public static byte EntityEnd = 0xFB;

        public static byte ComponentStart = 0xF1;
        public static byte ComponentEnd = 0xF2;
    }

    /*
     *  Int32  - EntityCount
     * Entity Record:
     *  Byte   - Entity Start         - 0xAA
     *  Int32  - Component count
     * -- This Component sub-record repeats
     * {
     *  Byte   - Component Start      - 0xBB
     *  String - Component Qualified Name
     *  Int32  - Component data length
     *  byte[] - Component data
     *  Byte   - Component End        - 0xBF
     * }
     *  Byte   - Entity End           - 0xAF
     */

    public class EntityReader {
        public IEnumerable<Entity> ReadFrom(string inPath, Func<IEnumerable<IComponent>, Entity> entityCreator) {
            using (var reader = new BinaryReader(new FileStream(inPath, FileMode.Open))) {
                var fileVersion = reader.ReadInt32();

                if (fileVersion != Constants.FileVersion) {
                    throw new ArgumentOutOfRangeException(nameof(inPath), $"Expected file version {Constants.FileVersion} but found {fileVersion}");
                }

                var entityCount = reader.ReadInt32();

                for (int i = 0; i <= entityCount; i++) {
                    var entityStart = reader.ReadByte();
                    Debug.Assert(entityStart == Constants.EntityStart, "entityStart == Constants.EntityStart");

                    var componentCount = reader.ReadInt32();
                    var entityComponents = new List<IComponent>(componentCount);

                    for (int j = 0; j <= componentCount; j++) {
                        var componentStart = reader.ReadByte();
                        Debug.Assert(componentStart == Constants.ComponentStart, "componentStart == Constants.ComponentStart");

                        // Initialize a new component object based on the saved name.
                        var componentTypeName = reader.ReadString();
                        var type = Type.GetType(componentTypeName);
                        if (type == null) {
                            throw new TypeInitializationException(componentTypeName, null);
                        }
                        var instance = Activator.CreateInstance(type);

                        // Treat it as a SerializableComponent
                        var component = instance as ISerializableComponent;

                        // Grab the component data.
                        var componentDataLength = reader.ReadInt32();
                        var componentData = reader.ReadBytes(componentDataLength);

                        // Ask the component to deserialize itself from provided data
                        Debug.Assert(component != null, nameof(component) + " != null");
                        component.Deserialize(componentData);

                        // Push it into the list
                        entityComponents.Add(component);

                        // Check we've hit the end of the record
                        var componentEnd = reader.ReadByte();
                        Debug.Assert(componentEnd == Constants.ComponentEnd, "componentEnd == Constants.ComponentEnd");
                    }

                    // Check we've hit the end of the entity record
                    var entityEnd = reader.ReadByte();
                    Debug.Assert(entityEnd == Constants.EntityEnd, "entityEnd == Constants.EntityEnd");

                    // Politely ask the user to create a new Entity based on the supplied components.
                    yield return entityCreator(entityComponents);
                }
            }
        }
    }

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
            byte[] toBytes(int i) {
                byte[] bytes1 = new byte[4];
                bytes1[0] = (byte) (i >> 24);
                bytes1[1] = (byte) (i >> 16);
                bytes1[2] = (byte) (i >> 8);
                bytes1[3] = (byte) i;
                return bytes1;
            }

            var ms = new MemoryStream();
            ms.Write(toBytes(Constants.FileVersion), 0, 4);
            ms.Write(toBytes(entities.Count()), 0, 4);

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