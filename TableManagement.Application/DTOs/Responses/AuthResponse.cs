﻿namespace TableManagement.Application.DTOs.Responses
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
        public UserDto User { get; set; }
    }

 
}