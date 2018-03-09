using System.Diagnostics;
using System.Threading.Tasks;

namespace Capsicum
{
    public abstract class EntitySystem
    {
        /// <summary>
        /// Initialize this system, if required.
        /// </summary>
        public virtual void Initialize() { }

        public void Process(Entity e)
        {
            if (CanProcess)
                ProcessSystem(e);
        }

        protected abstract void ProcessSystem(Entity e);

        public virtual bool CanProcess => true;
    }
}