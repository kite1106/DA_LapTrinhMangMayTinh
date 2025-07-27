using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.DTOs.User;

/// <summary>
/// DTO cho thông tin người dùng
/// </summary>
public record UserDto(
    string Id,
    string Email,
    string? FullName,
    bool IsEmailConfirmed,
    DateTime? LastLoginTime,
    int LoginCount,
    IEnumerable<string> Roles
);

/// <summary>
/// DTO cho cập nhật thông tin người dùng
/// </summary>
public record UpdateUserDto(
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải có ít nhất 2 ký tự")]
    string FullName,

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    string? NewEmail
);

/// <summary>
/// DTO cho thông tin người dùng tối thiểu
/// </summary>
public record UserInfoDto(
    string Id,
    string Email,
    string? FullName
);
