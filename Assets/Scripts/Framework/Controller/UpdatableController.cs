using System.Collections.Generic;
using UnityEngine;

namespace Framework.Controller
{
    public class UpdatableController<T> : BaseController<T> where T : UpdatableController<T>
    {
        [SerializeReference, SubclassSelector]
        public List<Updatable<T>> updatables = new List<Updatable<T>>();
        
        public void Start()
        {
            foreach (var updatable in updatables)
            {
                if (updatable != null) updatable.Start((T)(object)this);
            }
        }

        public void Update()
        {
            foreach (var updatable in updatables)
            {
                if (updatable != null) updatable.Update((T)(object)this);
            }
        }

        private void FixedUpdate()
        {
            foreach (var updatable in updatables)
            {
                if (updatable != null) updatable.FixedUpdate((T)(object)this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            foreach (var updatable in updatables)
            {
                if (updatable != null) updatable.OnTriggerEnter((T)(object)this, other);
            }
        }

        public void OnDrawGizmos()
        {
            foreach (var updatable in updatables)
            {
                if (updatable != null) updatable.OnDrawGizmos((T)(object)this);
            }
        }

        public void OnDestroy()
        {
            foreach (var updatable in updatables)
            {
                if (updatable != null) updatable.OnDestroy((T)(object)this);
            }
        }
    }
}