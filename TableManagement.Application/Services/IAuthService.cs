using Microsoft.AspNetCore.Mvc;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;
using TableManagement.Core.Entities;

namespace TableManagement.Application.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> ConfirmEmailAsync(string token, string email);
        Task<AuthResponse> ResendEmailConfirmationAsync(string email);
        string GenerateJwtToken(int userId, string userName, string email);

        Task<ServiceResponse> DeleteUserAsync(int Id);
        Task<int> GetCurrentUserId();
    }
}