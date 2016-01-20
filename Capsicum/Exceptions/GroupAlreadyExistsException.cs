using System;

namespace Capsicum.Exceptions {
    public class GroupAlreadyExistsException : Exception {
        public GroupAlreadyExistsException(string message) : base(message) {}
    }
}