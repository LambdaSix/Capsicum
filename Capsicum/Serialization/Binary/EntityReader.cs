using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Capsicum.Interfaces;

namespace Capsicum.Serialization.Binary {
    public class EntityReader {
        public IEnumerable<Entity> ReadFrom(string inPath, Func<IEnumerable<IComponent>, Entity> entityCreator) {
            using (var reader = new BinaryReader(new FileStream(inPath, FileMode.Open))) {
                foreach (var entity in ReadBuffer(entityCreator, reader)) yield return entity;
            }
        }

        public IEnumerable<Entity> ReadFrom(byte[] inBuffer, Func<IEnumerable<IComponent>, Entity> entityCreator) {
            using (var reader = new BinaryReader(new MemoryStream(inBuffer))) {
                foreach (var entity in ReadBuffer(entityCreator, reader)) yield return entity;
            }
        }

        private static IEnumerable<Entity> ReadBuffer(Func<IEnumerable<IComponent>, Entity> entityCreator, BinaryReader reader) {
            var fileVersion = reader.ReadInt32();

            if (fileVersion != Constants.FileVersion) {
                throw new ArgumentOutOfRangeException(nameof(reader),
                    $"Expected file version {Constants.FileVersion} but found {fileVersion}");
            }

            var entityCount = reader.ReadInt32();

            for (int i = 0; i < entityCount; i++) {
                var entityStart = reader.ReadByte();
                Debug.Assert(entityStart == Constants.EntityStart, "entityStart == Constants.EntityStart");

                var componentCount = reader.ReadInt32();
                var entityComponents = new List<IComponent>(componentCount);

                for (int j = 0; j < componentCount; j++) {
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