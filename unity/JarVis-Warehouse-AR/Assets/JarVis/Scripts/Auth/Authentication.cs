using System;
using System.Collections;
using JarVis.Warehouse.App;
using JarVis.Warehouse.Backend;
using UnityEngine;

namespace JarVis.Warehouse.Auth
{
    public sealed class AuthenticationRequest
    {
        public string EmployeeId { get; }
        public string AccessCode { get; }
        public WarehouseRole Role { get; }

        public AuthenticationRequest(string employeeId, string accessCode, WarehouseRole role)
        {
            EmployeeId = employeeId;
            AccessCode = accessCode;
            Role = role;
        }
    }

    public sealed class AuthenticationResult
    {
        public bool Success { get; }
        public string ErrorKey { get; }
        public string EmployeeCode { get; }

        public AuthenticationResult(bool success, string errorKey = null, string employeeCode = null)
        {
            Success = success;
            ErrorKey = errorKey;
            EmployeeCode = employeeCode;
        }
    }

    public sealed class OmsAuthenticationService : IAuthenticationService
    {
        private readonly string baseUrl;

        public OmsAuthenticationService(string baseUrl)
        {
            this.baseUrl = baseUrl;
        }

        public IEnumerator Authenticate(AuthenticationRequest request, Action<AuthenticationResult> completed)
        {
            if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.AccessCode))
            {
                completed?.Invoke(new AuthenticationResult(false, "login_required"));
                yield break;
            }
            OmsMobileApi.LoginReply reply = null;
            string error = null;
            yield return OmsMobileApi.Login(baseUrl, request.EmployeeId.Trim(), request.AccessCode,
                value => reply = value, value => error = value);
            if (reply == null || error != null)
            {
                completed?.Invoke(new AuthenticationResult(false, error != null && error.Contains("Invalid")
                    ? "login_invalid" : "network_error"));
                yield break;
            }
            OmsMobileSession.Set(baseUrl, reply.accessToken, reply.user.employeeCode, reply.expiresAt);
            completed?.Invoke(new AuthenticationResult(true, employeeCode: reply.user.employeeCode));
        }
    }

    public interface IAuthenticationService
    {
        IEnumerator Authenticate(AuthenticationRequest request, Action<AuthenticationResult> completed);
    }

    public sealed class MockAuthenticationService : IAuthenticationService
    {
        public IEnumerator Authenticate(AuthenticationRequest request, Action<AuthenticationResult> completed)
        {
            yield return new WaitForSecondsRealtime(0.55f);

            if (string.IsNullOrWhiteSpace(request.EmployeeId)
                || string.IsNullOrWhiteSpace(request.AccessCode))
            {
                completed?.Invoke(new AuthenticationResult(false, "login_required"));
                yield break;
            }

            if (request.AccessCode.Length < 4)
            {
                completed?.Invoke(new AuthenticationResult(false, "login_invalid"));
                yield break;
            }

            completed?.Invoke(new AuthenticationResult(true));
        }
    }
}
