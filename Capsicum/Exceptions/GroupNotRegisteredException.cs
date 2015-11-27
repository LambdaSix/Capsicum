using System;

namespace Capsicum.Exceptions {
    public class GroupNotRegisteredException : Exception {
        public GroupNotRegisteredException(string message) : base(message) {}
    }
}