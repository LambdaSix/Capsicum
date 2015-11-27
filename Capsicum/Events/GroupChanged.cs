using System;
using System.Collections.Generic;

namespace Capsicum.Events {
    public class GroupChanged : EventArgs {
        public Group Group { get; set; }
        public IEnumerable<Entity> Entities { get; set; }

        public GroupChanged(Group group, IEnumerable<Entity> entities) {
            Group = group;
            Entities = entities;
        }
    }
}