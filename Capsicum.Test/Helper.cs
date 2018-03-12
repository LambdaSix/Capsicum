using Capsicum.Interfaces;

namespace Capsicum.Test {
    public class Helper {
         
    }

    public class FakeComponent : IComponent
    {
        public string Name { get; set; }
    }

    public class OtherComponent : IComponent {
        public string Name { get; set; }
        public string Data { get; set; }
    }
}