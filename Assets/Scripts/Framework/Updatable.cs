using System;
using UnityEngine;

namespace Framework
{
    [Serializable]
    public abstract class Updatable<T>
    {
        public virtual void Start(T controller) {}
        public virtual void Update(T controller) {}
        public virtual void FixedUpdate(T controller) {}
        public virtual void OnTriggerEnter(T controller, Collider other) {}
        
        // Virtual cause there is no obligation to override it in fact
        public virtual void OnDrawGizmos(T controller) {}
        public virtual void OnDestroy(T controller) {}

        /// <summary>Appelé quand l'UpdatableController est activé (OnEnable).</summary>
        public virtual void OnEnabled(T controller) {}
        
        /// <summary>Appelé quand l'UpdatableController est désactivé (OnDisable).</summary>
        public virtual void OnDisabled(T controller) {}
    }
}