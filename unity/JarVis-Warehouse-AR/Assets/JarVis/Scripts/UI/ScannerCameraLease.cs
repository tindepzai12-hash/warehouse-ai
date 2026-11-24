using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace JarVis.Warehouse.UI
{
    public sealed class ScannerCameraLease
    {
        static ImportedCameraScanner owner;
        readonly List<Behaviour> suspended = new List<Behaviour>();
        ImportedCameraScanner holder;
        public bool Acquire(ImportedCameraScanner scanner)
        {
            if (owner != null && owner != scanner) return false;
            if (holder == scanner) return true;
            owner = holder = scanner;
            Suspend(Object.FindObjectsByType<ARCameraBackground>());
            Suspend(Object.FindObjectsByType<ARCameraManager>());
            Suspend(Object.FindObjectsByType<ARSession>());
            return true;
        }
        void Suspend<T>(T[] components) where T : Behaviour
        {
            foreach (var component in components)
                if (component.enabled) { suspended.Add(component); component.enabled = false; }
        }
        public bool CameraStopped
        {
            get
            {
                foreach (var component in suspended)
                    if (component is ARCameraManager manager && manager != null && manager.subsystem != null && manager.subsystem.running) return false;
                return true;
            }
        }
        public void Release()
        {
            if (holder == null && suspended.Count == 0) return;
            for (int index = suspended.Count - 1; index >= 0; index--)
                if (suspended[index] != null) suspended[index].enabled = true;
            suspended.Clear();
            if (owner == holder) owner = null;
            holder = null;
        }
    }
}
