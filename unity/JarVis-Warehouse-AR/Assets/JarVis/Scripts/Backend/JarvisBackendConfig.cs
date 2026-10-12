using UnityEngine;

namespace JarVis.Warehouse.Backend
{
    [CreateAssetMenu(fileName = "JarvisBackendConfig", menuName = "JarVis/Backend Config")]
    public sealed class JarvisBackendConfig : ScriptableObject
    {
        [SerializeField] private string baseUrl = string.Empty;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(baseUrl);

        public string GetEndpoint(string relativePath)
        {
            if (!IsConfigured)
                return string.Empty;

            return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
        }
    }
}
