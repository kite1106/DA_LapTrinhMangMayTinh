namespace SecurityMonitor.DTOs;

public record LoginRequest(
    string Username, 
    string Password, 
    bool RememberMe
);

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string ConfirmPassword,
    string? FullName,
    string? Department
);

public record AuthResponse(
    bool Succeeded,
    string? Token,
    string? Message,
    UserInfo? User
);

public record UserInfo(
    string Id,
    string Username,
    string Email,
    string? FullName,
    string? Department,
    List<string> Roles
);
