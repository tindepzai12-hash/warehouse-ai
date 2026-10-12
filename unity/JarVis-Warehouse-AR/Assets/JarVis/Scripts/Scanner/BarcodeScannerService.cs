using System;
using System.Collections;
using JarVis.Warehouse.Data;
using JarVis.Warehouse.Providers;
using UnityEngine;

namespace JarVis.Warehouse.Scanner
{
    public interface IBarcodeScannerService
    {
        bool HasDecoder { get; }
        WebCamTexture CameraTexture { get; }
        IEnumerator StartCamera(Action<bool, string> completed);
        void StopCamera();
        bool TryManualLookup(string barcode, out WarehouseItem item, out WarehouseTask task, out string errorKey);
    }

    public sealed class BarcodeScannerService : IBarcodeScannerService
    {
        private const float DuplicateWindowSeconds = 1.5f;
        private readonly MockWarehouseRepository repository;
        private string lastBarcode;
        private float lastAcceptedTime = float.NegativeInfinity;

        public bool HasDecoder => false;
        public WebCamTexture CameraTexture { get; private set; }

        public BarcodeScannerService(MockWarehouseRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IEnumerator StartCamera(Action<bool, string> completed)
        {
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                completed?.Invoke(false, "camera_permission_denied");
                yield break;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                completed?.Invoke(false, "camera_unavailable");
                yield break;
            }

            StopCamera();
            CameraTexture = new WebCamTexture(devices[0].name, 1280, 720, 30);
            CameraTexture.Play();
            completed?.Invoke(true, "decoder_not_installed");
        }

        public void StopCamera()
        {
            if (CameraTexture == null)
                return;

            if (CameraTexture.isPlaying)
                CameraTexture.Stop();
            UnityEngine.Object.Destroy(CameraTexture);
            CameraTexture = null;
        }

        public bool TryManualLookup(
            string barcode,
            out WarehouseItem item,
            out WarehouseTask task,
            out string errorKey
        )
        {
            item = null;
            task = null;
            errorKey = null;
            string normalized = barcode?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                errorKey = "barcode_required";
                return false;
            }

            if (string.Equals(normalized, lastBarcode, StringComparison.Ordinal)
                && Time.unscaledTime - lastAcceptedTime < DuplicateWindowSeconds)
            {
                errorKey = "duplicate_scan";
                return false;
            }

            lastBarcode = normalized;
            lastAcceptedTime = Time.unscaledTime;
            item = repository.FindByBarcode(normalized);
            task = repository.FindTaskByBarcode(normalized);
            if (item != null)
                return true;

            errorKey = "barcode_not_found";
            return false;
        }
    }
}
